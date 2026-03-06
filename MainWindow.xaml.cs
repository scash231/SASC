using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SASC.Models;
using SASC.Services;
using SASC.Views;
using System.Net.Http;

namespace SASC
{
    public partial class MainWindow : Window
    {
        private readonly AccountService _accountService = new();
        private readonly SteamAvatarService _avatarService = new();
        private readonly DiscordService _discordService = new();
        private readonly VencordService _vencordService = new();
        private SteamService _steamService;
        private AppSettings _settings;
        private List<SteamAccount> _steamAccounts = new();
        private Dictionary<string, SteamAccount> _manualAccounts = new();
        private List<DiscordAccount> _discordAccounts = new();
        private bool _manualExpanded = false;
        private string _activeTab = "steam";

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }

        public MainWindow()
        {
            InitializeComponent();
            _settings = _accountService.LoadSettings();
            _steamService = new SteamService(_settings.SteamPath);
            LoadAll();

            Loaded += (_, _) =>
            {
                SetTabStyle(TabSteamBorder, BtnTabSteam, active: true);
                SetTabStyle(TabEpicBorder, BtnTabEpic, active: false);
                SetTabStyle(TabDiscordBorder, BtnTabDiscord, active: false);
            };

            if (!string.IsNullOrEmpty(_settings.AutoLogin))
                Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(600);
                    await SwitchAccount(_settings.AutoLogin);
                });
        }

        public List<SteamAccount> GetAllAccounts()
        {
            var all = new List<SteamAccount>(_steamAccounts);
            all.AddRange(_manualAccounts.Values);
            return all;
        }

        public async Task SwitchAccountPublic(string username) =>
            await SwitchAccount(username);

        // ── Titlebar ──────────────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object s, MouseButtonEventArgs e) => DragMove();
        private void BtnMinimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnCloseWindow_Click(object s, RoutedEventArgs e) => Close();

        // ── Tab toggle ────────────────────────────────────────────────────────

        private static readonly List<string> TabOrder = new() { "steam", "epic", "discord" };

        private void BtnTabSteam_Click(object s, RoutedEventArgs e)
        {
            if (_activeTab == "steam") return;
            SwitchTab("steam", SteamScrollViewer, () =>
            {
                ManualHeaderRow.Height = new GridLength(24);
                Render();
            });
            SetTabStyle(TabSteamBorder, BtnTabSteam, active: true);
            SetTabStyle(TabEpicBorder, BtnTabEpic, active: false);
            SetTabStyle(TabDiscordBorder, BtnTabDiscord, active: false);
            TxtSectionLabel.Text = "STEAM";
        }

        private void BtnTabEpic_Click(object s, RoutedEventArgs e)
        {
            if (_activeTab == "epic") return;
            SwitchTab("epic", EpicScrollViewer, () =>
            {
                ManualHeaderRow.Height = new GridLength(0);
                ManualListRow.Height = new GridLength(0);
                EpicList.Children.Clear();
                EpicList.Children.Add(EmptyLabel("epic games support coming soon"));
                AdjustWindowHeight();
            });
            SetTabStyle(TabEpicBorder, BtnTabEpic, active: true);
            SetTabStyle(TabSteamBorder, BtnTabSteam, active: false);
            SetTabStyle(TabDiscordBorder, BtnTabDiscord, active: false);
            TxtSectionLabel.Text = "EPIC GAMES";
        }

        private void BtnTabDiscord_Click(object s, RoutedEventArgs e)
        {
            if (_activeTab == "discord") return;
            SwitchTab("discord", DiscordScrollViewer, () =>
            {
                ManualHeaderRow.Height = new GridLength(0);
                ManualListRow.Height = new GridLength(0);
                RenderDiscord();
            });
            SetTabStyle(TabDiscordBorder, BtnTabDiscord, active: true);
            SetTabStyle(TabSteamBorder, BtnTabSteam, active: false);
            SetTabStyle(TabEpicBorder, BtnTabEpic, active: false);
            TxtSectionLabel.Text = "DISCORD";
        }

        private void SwitchTab(string newTab, ScrollViewer inViewer, Action onReady)
        {
            var outViewer = GetActiveViewer();
            int oldIndex = TabOrder.IndexOf(_activeTab);
            int newIndex = TabOrder.IndexOf(newTab);
            double dir = newIndex > oldIndex ? 1 : -1;

            _activeTab = newTab;
            onReady();

            AnimateTabSwitch(
                outViewer: outViewer, outFrom: 0, outTo: -dir * 360,
                inViewer: inViewer, inFrom: dir * 360, inTo: 0);
        }

        private ScrollViewer GetActiveViewer() => _activeTab switch
        {
            "epic" => EpicScrollViewer,
            "discord" => DiscordScrollViewer,
            _ => SteamScrollViewer
        };

        // ── Tab style + animations ────────────────────────────────────────────

        private static void SetTabStyle(Border border, Button btn, bool active)
        {
            var dur = new Duration(TimeSpan.FromMilliseconds(200));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var currentColor = btn.Foreground is SolidColorBrush scb
                ? scb.Color
                : (active ? Color.FromRgb(0x33, 0x33, 0x33) : Color.FromRgb(0x58, 0x65, 0xf2));
            var targetColor = active
                ? Color.FromRgb(0x58, 0x65, 0xf2)
                : Color.FromRgb(0x33, 0x33, 0x33);
            var brush = new SolidColorBrush(currentColor);
            btn.Foreground = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(targetColor, dur) { EasingFunction = ease });

            var borderBrush = new SolidColorBrush(
                active
                    ? Color.FromArgb(0, 0x58, 0x65, 0xf2)
                    : Color.FromRgb(0x58, 0x65, 0xf2));
            border.BorderBrush = borderBrush;
            border.RenderTransform = null;

            var targetBorderColor = active
                ? Color.FromRgb(0x58, 0x65, 0xf2)
                : Color.FromArgb(0, 0x58, 0x65, 0xf2);
            borderBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(targetBorderColor, dur) { EasingFunction = ease });
        }

        private void AnimateTabSwitch(
            ScrollViewer outViewer, double outFrom, double outTo,
            ScrollViewer inViewer, double inFrom, double inTo)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(200));
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            outViewer.BeginAnimation(OpacityProperty, null);
            ((TranslateTransform)outViewer.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
            inViewer.BeginAnimation(OpacityProperty, null);
            ((TranslateTransform)inViewer.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);

            var fadeOut = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
            var slideOut = new DoubleAnimation(outFrom, outTo, duration) { EasingFunction = ease };
            fadeOut.Completed += (_, _) =>
            {
                outViewer.Visibility = Visibility.Collapsed;
                ((TranslateTransform)outViewer.RenderTransform).X = 0;
            };
            outViewer.BeginAnimation(OpacityProperty, fadeOut);
            ((TranslateTransform)outViewer.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideOut);

            inViewer.Opacity = 0;
            inViewer.Visibility = Visibility.Visible;
            ((TranslateTransform)inViewer.RenderTransform).X = inFrom;
            ((TranslateTransform)inViewer.RenderTransform).BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(inFrom, inTo, duration) { EasingFunction = ease });
            inViewer.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        }

        // ── Load & Render ─────────────────────────────────────────────────────

        private void LoadAll()
        {
            _steamAccounts = _steamService.ParseAccounts();
            _manualAccounts = _accountService.LoadManualAccounts();
            Render();

            int total = _steamAccounts.Count + _manualAccounts.Count;
            SetStatus($"{total} account{(total != 1 ? "s" : "")} loaded");
            App.RefreshTrayMenu();

            _ = FetchAndRefreshAvatarsAsync();
        }

        private async Task FetchAndRefreshAvatarsAsync()
        {
            var missing = _steamAccounts
                .Where(a => !string.IsNullOrEmpty(a.SteamId) &&
                            _avatarService.GetCachedPath(a.SteamId) == null)
                .Select(a => a.SteamId)
                .ToList();
            if (missing.Count == 0) return;
            await _avatarService.FetchAvatarsAsync(missing);
            Dispatcher.Invoke(Render);
        }

        private void Render()
        {
            if (_activeTab != "steam") return;
            if (_steamAccounts == null || _manualAccounts == null) return;
            if (SteamList == null || ManualList == null) return;

            SteamList.Children.Clear();
            ManualList.Children.Clear();

            var sorted = _steamAccounts.OrderBy(a => a.PersonaName).ToList();

            if (sorted.Count > 0)
                foreach (var acc in sorted)
                    SteamList.Children.Add(BuildRow(acc));
            else
                SteamList.Children.Add(EmptyLabel("no accounts found"));

            int manualCount = _manualAccounts.Count;
            TxtManualCount.Text = manualCount > 0 ? $"({manualCount})" : "";

            if (manualCount > 0)
            {
                foreach (var kv in _manualAccounts)
                    ManualList.Children.Add(BuildRow(kv.Value));
                if (!_manualExpanded)
                {
                    _manualExpanded = true;
                    UpdateManualVisibility();
                }
            }
            else
            {
                _manualExpanded = false;
                UpdateManualVisibility();
            }

            BtnToggleManual.Content = _manualExpanded ? "▾" : "▸";
            AdjustWindowHeight();
        }

        private void UpdateManualVisibility()
        {
            bool hasAccounts = _manualAccounts.Count > 0;
            ManualListRow.Height = _manualExpanded && hasAccounts
                ? new GridLength(140) : new GridLength(0);
            BtnToggleManual.Visibility = hasAccounts
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnToggleManual_Click(object s, RoutedEventArgs e)
        {
            _manualExpanded = !_manualExpanded;
            UpdateManualVisibility();
            BtnToggleManual.Content = _manualExpanded ? "▾" : "▸";
            AdjustWindowHeight();
        }

        // ── Row builder ───────────────────────────────────────────────────────

        private Border BuildRow(SteamAccount acc)
        {
            var root = new Border
            {
                Background = new SolidColorBrush(acc.IsRecent
                    ? Color.FromRgb(0x0d, 0x12, 0x1a)
                    : Color.FromRgb(0x0b, 0x0b, 0x14)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 1, 0, 1),
                Height = 40
            };
            root.MouseEnter += (_, _) =>
                root.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x20));
            root.MouseLeave += (_, _) =>
                root.Background = new SolidColorBrush(acc.IsRecent
                    ? Color.FromRgb(0x0d, 0x12, 0x1a)
                    : Color.FromRgb(0x0b, 0x0b, 0x14));

            var grid = new Grid { Margin = new Thickness(10, 0, 8, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                Background = new SolidColorBrush(acc.IsManual
                    ? Color.FromRgb(0x2a, 0x1a, 0x0a)
                    : Color.FromRgb(0x0d, 0x1a, 0x35))
            };

            string? cachedAvatar = !acc.IsManual && !string.IsNullOrEmpty(acc.SteamId)
                ? _avatarService.GetCachedPath(acc.SteamId) : null;

            if (cachedAvatar != null)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(Path.GetFullPath(cachedAvatar));
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 26;
                    bmp.EndInit();
                    avatarBorder.Child = new Image
                    {
                        Source = bmp,
                        Stretch = Stretch.UniformToFill,
                        Width = 26,
                        Height = 26
                    };
                }
                catch { avatarBorder.Child = MakeInitialBlock(acc); }
            }
            else { avatarBorder.Child = MakeInitialBlock(acc); }

            Grid.SetColumn(avatarBorder, 0);

            var nameStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            nameStack.Children.Add(new TextBlock
            {
                Text = acc.DisplayName,
                Foreground = new SolidColorBrush(acc.IsRecent
                    ? Color.FromRgb(0xe0, 0xe0, 0xff)
                    : Color.FromRgb(0x88, 0x88, 0xaa)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = acc.IsRecent ? FontWeights.Medium : FontWeights.Normal
            });
            string sub = acc.IsManual ? "manual" : $"@{acc.Username}";
            if (!string.IsNullOrEmpty(acc.Note)) sub += $" · {acc.Note}";
            nameStack.Children.Add(new TextBlock
            {
                Text = sub,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x44)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9
            });
            Grid.SetColumn(nameStack, 1);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            if (!acc.IsManual)
            {
                var btnId = MakeButton("id", "#0e0e1c", "#2a2a44", 24, 20, 9);
                btnId.Click += (_, _) =>
                {
                    Clipboard.SetText(acc.SteamId);
                    SetStatus($"copied: {acc.SteamId}");
                };
                btnPanel.Children.Add(btnId);

                var btnProfile = MakeButton("↗", "#0e0e1c", "#2a2a44", 24, 20, 10);
                btnProfile.Click += (_, _) =>
                    Process.Start(new ProcessStartInfo(
                        $"https://steamcommunity.com/profiles/{acc.SteamId}")
                    { UseShellExecute = true });
                btnPanel.Children.Add(btnProfile);
            }
            else
            {
                var btnEdit = MakeButton("edit", "#0e0e1c", "#2a2a44", 32, 20, 9);
                btnEdit.Click += (_, _) => OpenAccountDialog(acc.Username);
                btnPanel.Children.Add(btnEdit);

                var btnDel = MakeButton("✕", "#180a0a", "#442222", 20, 20, 9);
                btnDel.Click += (_, _) => DeleteManual(acc.Username);
                btnPanel.Children.Add(btnDel);
            }

            var switchBg = acc.IsRecent ? "#0a180a" : "#0c1220";
            var switchFg = acc.IsRecent ? "#1db954" : "#5865f2";
            var switchTxt = acc.IsRecent ? "active" : "switch";
            var btnSwitch = MakeButton(switchTxt, switchBg, switchFg, 52, 24, 10);
            btnSwitch.Margin = new Thickness(6, 0, 0, 0);
            btnSwitch.Click += async (_, _) =>
            {
                btnSwitch.Content = new ProgressBar
                {
                    Width = 36,
                    Height = 6,
                    IsIndeterminate = true,
                    Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(switchBg)!),
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(switchFg)!),
                    BorderThickness = new Thickness(0)
                };
                btnSwitch.IsEnabled = false;
                await SwitchAccount(acc.Username);
            };
            btnPanel.Children.Add(btnSwitch);

            Grid.SetColumn(btnPanel, 2);
            grid.Children.Add(avatarBorder);
            grid.Children.Add(nameStack);
            grid.Children.Add(btnPanel);
            root.Child = grid;
            return root;
        }

        // ── Discord ───────────────────────────────────────────────────────────

        private void RenderDiscord()
        {
            DiscordList.Children.Clear();

            // ── Status card ───────────────────────────────────────────────────
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xee, 0x05, 0x05, 0x0a)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(4, 16, 4, 8),
                Padding = new Thickness(20, 32, 20, 32),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Opacity = 0
            };
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = "⚠",
                Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0x65, 0xf2)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "under maintenance",
                Foreground = new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xcc)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = "discord account switching is coming soon",
                Foreground = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x44)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });
            card.Child = panel;
            DiscordList.Children.Add(card);

            // ── Vencord card ──────────────────────────────────────────────────
            string currentVariant = _settings.DiscordVariant ?? "Discord";
            bool installed = VencordService.IsInstalled(currentVariant);

            var vencordCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x18)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(4, 0, 4, 8),
                Padding = new Thickness(16, 14, 16, 14),
                Opacity = 0
            };
            var vPanel = new StackPanel();

            // Header
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var vTitle = new StackPanel { Orientation = Orientation.Horizontal };
            vTitle.Children.Add(new TextBlock
            {
                Text = "Vencord",
                Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0x65, 0xf2)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            vTitle.Children.Add(new TextBlock
            {
                Text = "  Discord mod",
                Foreground = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x44)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(vTitle, 0);
            var btnOpenSite = MakeButton("↗ website", "#0d0d20", "#2a2a55", 80, 22, 9);
            btnOpenSite.Click += (_, _) =>
                Process.Start(new ProcessStartInfo("https://vencord.dev") { UseShellExecute = true });
            Grid.SetColumn(btnOpenSite, 1);
            headerRow.Children.Add(vTitle);
            headerRow.Children.Add(btnOpenSite);
            vPanel.Children.Add(headerRow);

            // Badge
            var txtBadge = new TextBlock
            {
                Text = installed ? "● installed" : "● not installed",
                Foreground = new SolidColorBrush(installed
                    ? Color.FromRgb(0x1d, 0xb9, 0x54)
                    : Color.FromRgb(0x44, 0x44, 0x66)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                Margin = new Thickness(0, 6, 0, 0)
            };
            vPanel.Children.Add(txtBadge);

            // Progress
            var txtStatus = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0x65, 0xf2)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                Margin = new Thickness(0, 4, 0, 4)
            };
            vPanel.Children.Add(txtStatus);

            // ── Buttons vorab deklarieren ─────────────────────────────────────
            var btnInstall = MakeButton(installed ? "⟳  Reinstall / Repair" : "⬇  Install Vencord",
                                   "#0d0d25", "#5865f2", 190, 30, 11);
            var btnUninstall = MakeButton("✕ Uninstall", "#1a0a0a", "#cc4444", 90, 30, 11);
            btnUninstall.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;

            // ── Settings panel ────────────────────────────────────────────────
            var settingsPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0d, 0x0d, 0x20)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 8, 0, 10)
            };
            var settingsInner = new StackPanel();

            settingsInner.Children.Add(new TextBlock
            {
                Text = "SETTINGS",
                Foreground = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x55)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Auto-restart row
            var chkRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var chkDot = new TextBlock
            {
                Text = "●",
                Foreground = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x55)),
                FontSize = 7,
                Margin = new Thickness(0, 2, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var chkAutoRestart = new CheckBox
            {
                Content = "Restart Discord after install / uninstall",
                IsChecked = _settings.DiscordAutoRestart,
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x88)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10
            };
            chkAutoRestart.Checked += (_, _) => { _settings.DiscordAutoRestart = true; _accountService.SaveSettings(_settings); };
            chkAutoRestart.Unchecked += (_, _) => { _settings.DiscordAutoRestart = false; _accountService.SaveSettings(_settings); };
            chkRow.Children.Add(chkDot);
            chkRow.Children.Add(chkAutoRestart);
            settingsInner.Children.Add(chkRow);

            // Variant row
            var variantRow = new StackPanel { Orientation = Orientation.Horizontal };
            variantRow.Children.Add(new TextBlock
            {
                Text = "Variant",
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x77)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Width = 54,
                VerticalAlignment = VerticalAlignment.Center
            });
            var cmbVariant = new ComboBox
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Height = 26,
                MinWidth = 160,
                Background = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x99))
            };
            foreach (var v in new[] { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" })
                cmbVariant.Items.Add(v);
            cmbVariant.SelectedItem = currentVariant;
            if (cmbVariant.SelectedItem == null) cmbVariant.SelectedIndex = 0;
            cmbVariant.SelectionChanged += (_, _) =>
            {
                if (cmbVariant.SelectedItem is string v)
                {
                    _settings.DiscordVariant = v;
                    _accountService.SaveSettings(_settings);
                    bool nowInstalled = VencordService.IsInstalled(v);
                    txtBadge.Text = nowInstalled ? "● installed" : "● not installed";
                    txtBadge.Foreground = new SolidColorBrush(nowInstalled
                        ? Color.FromRgb(0x1d, 0xb9, 0x54)
                        : Color.FromRgb(0x44, 0x44, 0x66));
                    btnInstall.Content = nowInstalled ? "⟳  Reinstall / Repair" : "⬇  Install Vencord";
                    btnUninstall.Visibility = nowInstalled ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            variantRow.Children.Add(cmbVariant);
            settingsInner.Children.Add(variantRow);
            settingsPanel.Child = settingsInner;
            vPanel.Children.Add(settingsPanel);

            // ── Button click handlers ─────────────────────────────────────────
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            btnInstall.Click += async (_, _) =>
            {
                btnInstall.IsEnabled = btnUninstall.IsEnabled = false;
                string variant = _settings.DiscordVariant ?? "Discord";
                var progress = new Progress<string>(msg => Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = msg;
                    txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0x65, 0xf2));
                }));
                try
                {
                    await _vencordService.InstallAsync(progress, variant);
                    Dispatcher.Invoke(() =>
                    {
                        txtBadge.Text = "● installed";
                        txtBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x1d, 0xb9, 0x54));
                        txtStatus.Text = "installiert ✓";
                        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x1d, 0xb9, 0x54));
                        btnInstall.Content = "⟳  Reinstall / Repair";
                        btnUninstall.Visibility = Visibility.Visible;
                    });
                    if (_settings.DiscordAutoRestart)
                    {
                        await Task.Delay(600);
                        Dispatcher.Invoke(() => txtStatus.Text = "discord wird gestartet...");
                        VencordService.LaunchDiscord(variant);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = $"fehler: {ex.Message}";
                        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xcc, 0x44, 0x44));
                    });
                }
                btnInstall.IsEnabled = btnUninstall.IsEnabled = true;
            };

            btnUninstall.Click += async (_, _) =>
            {
                btnInstall.IsEnabled = btnUninstall.IsEnabled = false;
                string variant = _settings.DiscordVariant ?? "Discord";
                var progress = new Progress<string>(msg => Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = msg;
                    txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0x65, 0xf2));
                }));
                try
                {
                    await VencordService.UninstallAsync(progress, variant);
                    Dispatcher.Invoke(() =>
                    {
                        txtBadge.Text = "● not installed";
                        txtBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66));
                        txtStatus.Text = "deinstalliert ✓";
                        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x1d, 0xb9, 0x54));
                        btnInstall.Content = "⬇  Install Vencord";
                        btnUninstall.Visibility = Visibility.Collapsed;
                    });
                    if (_settings.DiscordAutoRestart)
                    {
                        await Task.Delay(600);
                        Dispatcher.Invoke(() => txtStatus.Text = "discord wird gestartet...");
                        VencordService.LaunchDiscord(variant);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = $"fehler: {ex.Message}";
                        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xcc, 0x44, 0x44));
                    });
                }
                btnInstall.IsEnabled = btnUninstall.IsEnabled = true;
            };

            btnRow.Children.Add(btnInstall);
            btnRow.Children.Add(btnUninstall);
            vPanel.Children.Add(btnRow);
            vencordCard.Child = vPanel;
            DiscordList.Children.Add(vencordCard);

            // ── Animate ───────────────────────────────────────────────────────
            var dur = new Duration(TimeSpan.FromMilliseconds(300));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            card.RenderTransform = new TranslateTransform(0, 16);
            card.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
            ((TranslateTransform)card.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(16, 0, dur) { EasingFunction = ease });

            vencordCard.RenderTransform = new TranslateTransform(0, 16);
            vencordCard.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, dur) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(80) });
            ((TranslateTransform)vencordCard.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(16, 0, dur) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(80) });

            AdjustWindowHeight();
        }

        // ── Dynamic height ────────────────────────────────────────────────────

        private void AdjustWindowHeight()
        {
            const double titleBar = 46;
            const double tabBar = 38;
            const double sectionLabel = 16;
            const double statusBar = 32;
            const double rowHeight = 42;
            const double padding = 20;

            double listHeight = _activeTab switch
            {
                "discord" => 420,
                "epic" => 220,
                _ => Math.Clamp((_steamAccounts?.Count ?? 0) * rowHeight, 80, 400)
            };

            double manualHeight = (_activeTab == "steam")
                ? (_manualExpanded && _manualAccounts?.Count > 0 ? 140 : 24)
                : 0;

            double total = titleBar + tabBar + sectionLabel
                         + listHeight + 1 + manualHeight + statusBar + padding;

            BeginAnimation(HeightProperty, new DoubleAnimation(
                Height, Math.Clamp(total, 300, 780),
                new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static TextBlock MakeInitialBlock(SteamAccount acc) => new()
        {
            Text = acc.Initials,
            Foreground = new SolidColorBrush(acc.IsManual
                ? Color.FromRgb(0xaa, 0x77, 0x33)
                : Color.FromRgb(0x58, 0x65, 0xf2)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        private static Button MakeButton(string text, string bg, string fg, int w, int h, int fs) =>
            new()
            {
                Content = text,
                Width = w,
                Height = h,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = fs,
                Foreground = new SolidColorBrush((Color)new ColorConverter().ConvertFrom(fg)!),
                Background = new SolidColorBrush((Color)new ColorConverter().ConvertFrom(bg)!),
                Margin = new Thickness(2, 0, 0, 0)
            };

        private static TextBlock EmptyLabel(string text) => new()
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x2e)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            Margin = new Thickness(4, 8, 0, 8)
        };

        // ── Steam Switch ──────────────────────────────────────────────────────

        private async Task SwitchAccount(string username)
        {
            var manual = _manualAccounts.ContainsKey(username)
                ? _manualAccounts[username] : null;
            SetStatus($"switching → {username}");

            await Task.Run(async () =>
            {
                await _steamService.KillSteamAsync(_settings.KillWait);
                if (manual != null)
                    _steamService.LaunchSteamWithLogin(username, manual.Password, _settings.LaunchArgs);
                else
                {
                    _steamService.PatchVdf(username);
                    _steamService.SetRegistry(username);
                    _steamService.LaunchSteam(_settings.LaunchArgs);
                }
            });

            var persona = _steamAccounts
                .FirstOrDefault(a => a.Username == username)?.PersonaName ?? username;
            SetStatus($"launched → {persona}");

            if (_settings.MinimizeOnSwitch)
                Dispatcher.Invoke(() => WindowState = WindowState.Minimized);

            await Task.Delay(1200);
            Dispatcher.Invoke(LoadAll);
        }

        // ── Manual accounts ───────────────────────────────────────────────────

        private void OpenAccountDialog(string username = "")
        {
            var existing = username != "" && _manualAccounts.ContainsKey(username)
                ? _manualAccounts[username] : null;

            var dlg = new AccountDialog(username, existing?.Password ?? "", existing?.Note ?? "")
            { Owner = this };

            if (dlg.ShowDialog() == true)
            {
                if (!string.IsNullOrEmpty(username) && username != dlg.ResultUsername)
                    _manualAccounts.Remove(username);
                _manualAccounts[dlg.ResultUsername] = new SteamAccount
                {
                    Username = dlg.ResultUsername,
                    Password = dlg.ResultPassword,
                    Note = dlg.ResultNote,
                    IsManual = true
                };
                _accountService.SaveManualAccounts(_manualAccounts);
                Render();
                SetStatus($"saved '{dlg.ResultUsername}'");
            }
        }

        private void DeleteManual(string username)
        {
            _manualAccounts.Remove(username);
            _accountService.SaveManualAccounts(_manualAccounts);
            Render();
            SetStatus($"removed '{username}'");
        }

        // ── Import / Export ───────────────────────────────────────────────────

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON files|*.json" };
            if (dlg.ShowDialog() == true)
            {
                var imported = _accountService.ImportAccounts(dlg.FileName);
                foreach (var kv in imported) _manualAccounts[kv.Key] = kv.Value;
                _accountService.SaveManualAccounts(_manualAccounts);
                Render();
                SetStatus($"imported {imported.Count} account(s)");
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JSON files|*.json",
                FileName = "scash_backup.json"
            };
            if (dlg.ShowDialog() == true)
            {
                _accountService.ExportAccounts(_manualAccounts, dlg.FileName);
                SetStatus($"exported → {System.IO.Path.GetFileName(dlg.FileName)}");
            }
        }

        // ── Settings ──────────────────────────────────────────────────────────

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_settings, _steamAccounts) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _settings = win.ResultSettings;
                _steamService = new SteamService(_settings.SteamPath);
                _accountService.SaveSettings(_settings);

                if (win.ShouldExit)
                {
                    App.TrayIcon?.Dispose();
                    Application.Current.Shutdown();
                    return;
                }

                LoadAll();
                SetStatus("settings saved");
            }
        }

        // ── Close behaviour ───────────────────────────────────────────────────

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_settings.CloseAction == "exit")
            {
                App.TrayIcon?.Dispose();
                Application.Current.Shutdown();
            }
            else
            {
                e.Cancel = true;
                Hide();
            }
        }

        // ── Misc ──────────────────────────────────────────────────────────────

        private void SetStatus(string msg) =>
            Dispatcher.Invoke(() => TxtStatus.Text = msg);

        private void BtnRefresh_Click(object s, RoutedEventArgs e) => LoadAll();
        private void BtnAddAccount_Click(object s, RoutedEventArgs e) => OpenAccountDialog();
    }
}
