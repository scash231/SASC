using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using SASC.Models;
using SASC.Views;

namespace SASC
{
    public partial class App : Application
    {
        public const string Version = "0.1-beta";
        public static TaskbarIcon? TrayIcon { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show(ex.Exception.ToString(), "SASC Crash",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            TrayIcon = (TaskbarIcon)FindResource("TrayIcon");
            TrayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
            TrayIcon.TrayLeftMouseUp      += (_, _) => ShowMainWindow();

            // Build initial context menu
            RefreshTrayMenu();

            ShowMainWindow();
        }

        public static void ShowMainWindow()
        {
            if (Current.MainWindow is not MainWindow win)
            {
                win = new MainWindow();
                Current.MainWindow = win;
            }
            win.Show();
            win.WindowState = WindowState.Normal;
            win.Activate();
        }

        // Called by MainWindow after every LoadAll() so menu stays in sync
        public static void RefreshTrayMenu()
        {
            if (TrayIcon == null) return;

            var accounts = (Current.MainWindow is MainWindow win)
                ? win.GetAllAccounts()
                : new List<SteamAccount>();

            var menu = new ContextMenu();

            // ── Quick switch accounts ──
            if (accounts.Count > 0)
            {
                var header = new MenuItem
                {
                    Header      = "Quick Switch",
                    IsEnabled   = false,
                    FontSize    = 11,
                    Foreground  = System.Windows.Media.Brushes.Gray
                };
                menu.Items.Add(header);

                foreach (var acc in accounts)
                {
                    var item = new MenuItem
                    {
                        Header = acc.IsRecent
                            ? $"● {acc.DisplayName}"
                            : $"○ {acc.DisplayName}"
                    };
                    var username = acc.Username;
                    item.Click += async (_, _) =>
                    {
                        if (Current.MainWindow is MainWindow w)
                            await w.SwitchAccountPublic(username);
                    };
                    menu.Items.Add(item);
                }

                menu.Items.Add(new Separator());
            }

            // ── Open / Exit ──
            var open = new MenuItem { Header = "Open SAS" };
            open.Click += (_, _) => ShowMainWindow();

            var exit = new MenuItem { Header = "Exit" };
            exit.Click += (_, _) =>
            {
                TrayIcon?.Dispose();
                Current.Shutdown();
            };

            menu.Items.Add(open);
            menu.Items.Add(new Separator());
            menu.Items.Add(exit);

            TrayIcon.ContextMenu = menu;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TrayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
