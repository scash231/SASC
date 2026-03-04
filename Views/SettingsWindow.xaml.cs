using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SASC.Models;
using SASC.Services;

namespace SASC.Views
{
    public partial class SettingsWindow : Window
    {
        public AppSettings ResultSettings { get; private set; }
        private readonly AccountService _accountService = new();

        public SettingsWindow(AppSettings settings, List<SteamAccount> steamAccounts)
        {
            InitializeComponent();
            ResultSettings = settings;

            // Close action
            RbTray.IsChecked = settings.CloseAction != "exit";
            RbExit.IsChecked = settings.CloseAction == "exit";

            ChkMinimize.IsChecked  = settings.MinimizeOnSwitch;
            ChkAutoStart.IsChecked = _accountService.IsAutoStartEnabled();
            TxtKillWait.Text       = settings.KillWait.ToString();
            TxtSteamPath.Text      = settings.SteamPath;
            TxtLaunchArgs.Text     = settings.LaunchArgs;
            TxtVersion.Text        = $"v{App.Version}";

            CmbAutoLogin.Items.Add("(disabled)");
            foreach (var acc in steamAccounts)
                CmbAutoLogin.Items.Add(acc.Username);
            CmbAutoLogin.SelectedItem = string.IsNullOrEmpty(settings.AutoLogin)
                ? "(disabled)" : settings.AutoLogin;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _accountService.SetAutoStart(ChkAutoStart.IsChecked == true);

            ResultSettings = new AppSettings
            {
                CloseAction      = RbExit.IsChecked == true ? "exit" : "tray",
                MinimizeOnSwitch = ChkMinimize.IsChecked == true,
                SteamPath        = TxtSteamPath.Text.Trim(),
                LaunchArgs       = TxtLaunchArgs.Text.Trim(),
                AutoLogin        = CmbAutoLogin.SelectedItem?.ToString() == "(disabled)"
                                   ? "" : CmbAutoLogin.SelectedItem?.ToString() ?? ""
            };
            if (int.TryParse(TxtKillWait.Text, out int w))
                ResultSettings.KillWait = w;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) =>
            DialogResult = false;
    }
}
