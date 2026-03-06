using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using SASC.Models;
using SASC.Services;

namespace SASC.Views
{
    public partial class SettingsWindow : Window
    {
        public AppSettings ResultSettings { get; private set; }
        public bool        ShouldExit     { get; private set; } = false;

        private readonly AccountService _accountService  = new();
        private bool                    _tabInitialized  = false;

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

        public SettingsWindow(AppSettings settings, List<SteamAccount> steamAccounts)
        {
            InitializeComponent();
            ResultSettings = settings;

            RbTray.IsChecked       = settings.CloseAction != "exit";
            RbExit.IsChecked       = settings.CloseAction == "exit";
            ChkMinimize.IsChecked  = settings.MinimizeOnSwitch;
            ChkAutoStart.IsChecked = _accountService.IsAutoStartEnabled();
            TxtKillWait.Text       = settings.KillWait.ToString();
            TxtSteamPath.Text      = settings.SteamPath;
            TxtLaunchArgs.Text     = settings.LaunchArgs;
            TxtEpicPath.Text       = settings.EpicPath;
            TxtVersion.Text        = $"v{App.Version}";

            CmbAutoLogin.Items.Add("(disabled)");
            foreach (var acc in steamAccounts)
                CmbAutoLogin.Items.Add(acc.Username);
            CmbAutoLogin.SelectedItem = string.IsNullOrEmpty(settings.AutoLogin)
                ? "(disabled)" : settings.AutoLogin;
        }

        // ── Tab transition animation ──────────────────────────────────────────

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_tabInitialized) { _tabInitialized = true; return; }

            var dur  = new Duration(TimeSpan.FromMilliseconds(220));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0.55,
                KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)))
                { EasingFunction = ease });

            TabTransitionOverlay.BeginAnimation(OpacityProperty, anim);
        }

        // ── Build & save ──────────────────────────────────────────────────────

        private AppSettings BuildSettings()
        {
            _accountService.SetAutoStart(ChkAutoStart.IsChecked == true);
            var s = new AppSettings
            {
                CloseAction      = RbExit.IsChecked == true ? "exit" : "tray",
                MinimizeOnSwitch = ChkMinimize.IsChecked == true,
                SteamPath        = TxtSteamPath.Text.Trim(),
                LaunchArgs       = TxtLaunchArgs.Text.Trim(),
                EpicPath         = TxtEpicPath.Text.Trim(),
                AutoLogin        = CmbAutoLogin.SelectedItem?.ToString() == "(disabled)"
                                   ? "" : CmbAutoLogin.SelectedItem?.ToString() ?? ""
            };
            if (int.TryParse(TxtKillWait.Text, out int w)) s.KillWait = w;
            return s;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ResultSettings = BuildSettings();
            DialogResult   = true;
        }


        private void Btnforcequit_Click(object sender, RoutedEventArgs e)
        {
            ShouldExit = true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) =>
            DialogResult = false;

        private void TitleBar_MouseLeftButtonDown(object s, MouseButtonEventArgs e) => DragMove();
    }
}
