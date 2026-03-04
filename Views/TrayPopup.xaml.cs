using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SASC.Models;

namespace SASC.Views
{
    public partial class TrayPopup : UserControl
    {
        private readonly Action<string> _onSwitch;

        public TrayPopup(List<SteamAccount> accounts, Action<string> onSwitch)
        {
            InitializeComponent();
            _onSwitch = onSwitch;
            BuildList(accounts);
        }

        private void BuildList(List<SteamAccount> accounts)
        {
            AccountList.Children.Clear();

            if (accounts.Count == 0)
            {
                AccountList.Children.Add(new TextBlock
                {
                    Text       = "No accounts found.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize   = 11,
                    Margin     = new Thickness(8, 6, 0, 6)
                });
                return;
            }

            foreach (var acc in accounts)
            {
                var row = new Border
                {
                    Height       = 36,
                    CornerRadius = new CornerRadius(6),
                    Margin       = new Thickness(0, 1, 0, 1),
                    Background   = new SolidColorBrush(acc.IsRecent
                        ? Color.FromRgb(0x1a, 0x3d, 0x1a)
                        : Color.FromRgb(0x13, 0x17, 0x1f)),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var grid = new Grid { Margin = new Thickness(8, 0, 8, 0) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var dot = new Ellipse
                {
                    Width  = 7, Height = 7,
                    Fill   = new SolidColorBrush(acc.IsRecent
                        ? Color.FromRgb(0x1d, 0xb9, 0x54)
                        : Color.FromRgb(0x3a, 0x3a, 0x3a)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 0);

                var name = new TextBlock
                {
                    Text              = acc.DisplayName,
                    Foreground        = Brushes.White,
                    FontFamily        = new FontFamily("Segoe UI"),
                    FontSize          = 12,
                    FontWeight        = acc.IsRecent ? FontWeights.SemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming      = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(name, 1);

                grid.Children.Add(dot);
                grid.Children.Add(name);
                row.Child = grid;

                var username = acc.Username;
                row.MouseLeftButtonUp += (_, _) => _onSwitch(username);
                row.MouseEnter += (_, _) =>
                    row.Background = new SolidColorBrush(Color.FromRgb(0x1f, 0x53, 0x8d));
                row.MouseLeave += (_, _) =>
                    row.Background = new SolidColorBrush(acc.IsRecent
                        ? Color.FromRgb(0x1a, 0x3d, 0x1a)
                        : Color.FromRgb(0x13, 0x17, 0x1f));

                AccountList.Children.Add(row);
            }
        }

        private void BtnOpen_Click(object s, RoutedEventArgs e) => App.ShowMainWindow();
        private void BtnExit_Click(object s, RoutedEventArgs e)
        {
            App.TrayIcon?.Dispose();
            Application.Current.Shutdown();
        }
    }
}
