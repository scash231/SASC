using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SASC.Models;
using SASC.Services;
using SASC.Views;

namespace SASC
{
    public partial class MainWindow : Window
    {
        private readonly AccountService _accountService = new();
        private SteamService            _steamService;
        private AppSettings             _settings;
        private List<SteamAccount>      _steamAccounts  = new();
        private Dictionary<string, SteamAccount> _manualAccounts = new();
        private bool _manualExpanded = false;

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
            _settings     = _accountService.LoadSettings();
            _steamService = new SteamService(_settings.SteamPath);
            LoadAll();

            if (!string.IsNullOrEmpty(_settings.AutoLogin))
                Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(600);
                    await SwitchAccount(_settings.AutoLogin);
                });
        }

        // ── Public API for tray ───────────────────────────────────────────────

        public List<SteamAccount> GetAllAccounts()
        {
            var all = new List<SteamAccount>(_steamAccounts);
            all.AddRange(_manualAccounts.Values);
            return all;
        }

        public async Task SwitchAccountPublic(string username) =>
            await SwitchAccount(username);

        // ── Load & Render ─────────────────────────────────────────────────────

        private void LoadAll()
        {
            _steamAccounts  = _steamService.ParseAccounts();
            _manualAccounts = _accountService.LoadManualAccounts();
            Render();
            int total = _steamAccounts.Count + _manualAccounts.Count;
            SetStatus($"{total} account{(total != 1 ? "s" : "")} loaded");
            App.RefreshTrayMenu();
        }

        private void Render()
        {
            if (_steamAccounts == null || _manualAccounts == null) return;
            if (SteamList == null || ManualList == null) return;

            string query  = TxtSearch.Text.ToLower();
            string sortBy = (CmbSort.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "name";

            SteamList.Children.Clear();
            ManualList.Children.Clear();

            var filtered = _steamAccounts
                .Where(a => a.Username.ToLower().Contains(query) ||
                            a.PersonaName.ToLower().Contains(query)).ToList();

            filtered = sortBy switch
            {
                "username" => filtered.OrderBy(a => a.Username).ToList(),
                "recent"   => filtered.OrderByDescending(a => a.IsRecent).ToList(),
                _          => filtered.OrderBy(a => a.PersonaName).ToList()
            };

            if (filtered.Count > 0)
                foreach (var acc in filtered)
                    SteamList.Children.Add(BuildRow(acc));
            else
                SteamList.Children.Add(EmptyLabel("no steam accounts found"));

            var filteredManual = _manualAccounts
                .Where(kv => kv.Key.ToLower().Contains(query)).ToList();

            int manualCount = filteredManual.Count;
            TxtManualCount.Text = manualCount > 0 ? $"({manualCount})" : "";

            if (manualCount > 0)
            {
                foreach (var kv in filteredManual)
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
        }

        private void UpdateManualVisibility()
        {
            bool hasAccounts = _manualAccounts.Count > 0;
            ManualListRow.Height   = _manualExpanded && hasAccounts
                ? new GridLength(140)
                : new GridLength(0);
            BtnToggleManual.Visibility = hasAccounts
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnToggleManual_Click(object s, RoutedEventArgs e)
        {
            _manualExpanded = !_manualExpanded;
            UpdateManualVisibility();
            BtnToggleManual.Content = _manualExpanded ? "▾" : "▸";
        }

        // ── Row builder ───────────────────────────────────────────────────────

        private Border BuildRow(SteamAccount acc)
        {
            var root = new Border
            {
                Background   = new SolidColorBrush(acc.IsRecent
                    ? Color.FromRgb(0x0d, 0x12, 0x1a)
                    : Color.FromRgb(0x0b, 0x0b, 0x14)),
                CornerRadius = new CornerRadius(6),
                Margin       = new Thickness(0, 1, 0, 1),
                Height       = 40
            };

            root.MouseEnter += (_, _) =>
                root.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x20));
            root.MouseLeave += (_, _) =>
                root.Background = new SolidColorBrush(acc.IsRecent
                    ? Color.FromRgb(0x0d, 0x12, 0x1a)
                    : Color.FromRgb(0x0b, 0x0b, 0x14));

            var grid = new Grid { Margin = new Thickness(10, 0, 8, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = new Border
            {
                Width = 24, Height = 24,
                CornerRadius      = new CornerRadius(12),
                Background        = new SolidColorBrush(acc.IsManual
                    ? Color.FromRgb(0x2a, 0x1a, 0x0a)
                    : Color.FromRgb(0x0d, 0x1a, 0x35)),
                VerticalAlignment = VerticalAlignment.Center
            };
            avatar.Child = new TextBlock
            {
                Text                = acc.Initials[..1],
                Foreground          = new SolidColorBrush(acc.IsManual
                    ? Color.FromRgb(0xaa, 0x77, 0x33)
                    : Color.FromRgb(0x58, 0x65, 0xf2)),
                FontFamily          = new FontFamily("Segoe UI"),
                FontSize            = 10, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            Grid.SetColumn(avatar, 0);

            var nameStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(6, 0, 0, 0)
            };
            nameStack.Children.Add(new TextBlock
            {
                Text       = acc.DisplayName,
                Foreground = new SolidColorBrush(acc.IsRecent
                    ? Color.FromRgb(0xe0, 0xe0, 0xff)
                    : Color.FromRgb(0x88, 0x88, 0xaa)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 12,
                FontWeight = acc.IsRecent ? FontWeights.Medium : FontWeights.Normal
            });
            string sub = acc.IsManual ? "manual" : $"@{acc.Username}";
            if (!string.IsNullOrEmpty(acc.Note)) sub += $" · {acc.Note}";
            nameStack.Children.Add(new TextBlock
            {
                Text       = sub,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x44)),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 9
            });
            Grid.SetColumn(nameStack, 1);

            var btnPanel = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                VerticalAlignment   = VerticalAlignment.Center,
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

            var switchBg  = acc.IsRecent ? "#0a180a" : "#0c1220";
            var switchFg  = acc.IsRecent ? "#1db954" : "#5865f2";
            var switchTxt = acc.IsRecent ? "active" : "switch";
            var btnSwitch = MakeButton(switchTxt, switchBg, switchFg, 52, 24, 10);
            btnSwitch.Margin = new Thickness(6, 0, 0, 0);
            btnSwitch.Click += async (_, _) => await SwitchAccount(acc.Username);
            btnPanel.Children.Add(btnSwitch);

            Grid.SetColumn(btnPanel, 2);
            grid.Children.Add(avatar);
            grid.Children.Add(nameStack);
            grid.Children.Add(btnPanel);
            root.Child = grid;
            return root;
        }

        private static Button MakeButton(string text, string bg, string fg, int w, int h, int fs) =>
            new()
            {
                Content    = text, Width = w, Height = h,
                FontFamily = new FontFamily("Segoe UI"), FontSize = fs,
                Foreground = new SolidColorBrush((Color)new ColorConverter().ConvertFrom(fg)!),
                Background = new SolidColorBrush((Color)new ColorConverter().ConvertFrom(bg)!),
                Margin     = new Thickness(2, 0, 0, 0)
            };

        private static TextBlock EmptyLabel(string text) => new()
        {
            Text       = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x2e)),
            FontFamily = new FontFamily("Segoe UI"), FontSize = 10,
            Margin     = new Thickness(4, 8, 0, 8)
        };

        // ── Switch ────────────────────────────────────────────────────────────

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

            // Only minimize if that setting is on — never auto-hide
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
                    Note     = dlg.ResultNote,
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
                Filter = "JSON files|*.json", FileName = "sas_backup.json"
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
                _settings     = win.ResultSettings;
                _steamService = new SteamService(_settings.SteamPath);
                _accountService.SaveSettings(_settings);
                LoadAll();
                SetStatus("settings saved");
            }
        }

        // ── Close behaviour (X button only) ──────────────────────────────────

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

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string msg) =>
            Dispatcher.Invoke(() => TxtStatus.Text = msg);

        private void TxtSearch_TextChanged(object s, TextChangedEventArgs e)
        {
            if (_steamAccounts == null) return;
            Render();
        }

        private void CmbSort_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (_steamAccounts == null) return;
            Render();
        }

        private void BtnRefresh_Click(object s, RoutedEventArgs e)    => LoadAll();
        private void BtnAddAccount_Click(object s, RoutedEventArgs e) => OpenAccountDialog();
    }
}
