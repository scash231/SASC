using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SASC.Views
{
    public class AccountDialog : Window
    {
        public string ResultUsername { get; private set; } = "";
        public string ResultPassword { get; private set; } = "";
        public string ResultNote     { get; private set; } = "";

        private readonly TextBox     _userBox;
        private readonly PasswordBox _passBox;
        private readonly TextBox     _noteBox;

        public AccountDialog(string username = "", string password = "", string note = "")
        {
            Title                 = username == "" ? "Add Account" : "Edit Account";
            Width                 = 380; Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode            = ResizeMode.NoResize;
            Background            = new SolidColorBrush(Color.FromRgb(0x0f, 0x11, 0x17));

            var stack = new StackPanel { Margin = new Thickness(24) };

            stack.Children.Add(new TextBlock
            {
                Text       = username == "" ? "Add Account" : "Edit Account",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 15, FontWeight = FontWeights.SemiBold,
                Margin     = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text       = "Stored locally in accounts.json",
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 10, Margin = new Thickness(0, 0, 0, 16)
            });

            _userBox = MakeTextBox(username, "Steam username");
            _passBox = new PasswordBox
            {
                Height      = 36, Margin = new Thickness(0, 0, 0, 8),
                Background  = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x2e)),
                Foreground  = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Padding     = new Thickness(8, 6, 8, 6),
                FontFamily  = new FontFamily("Segoe UI"), FontSize = 13
            };
            _passBox.Password = password;
            _noteBox = MakeTextBox(note, "Note (optional — e.g. 'main', 'alt')");

            stack.Children.Add(_userBox);
            stack.Children.Add(_passBox);
            stack.Children.Add(_noteBox);

            var showPass = new CheckBox
            {
                Content    = "Show password",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 11, Margin = new Thickness(0, 4, 0, 16)
            };
            showPass.Checked   += (_, _) => { /* can't show PasswordBox content easily */ };
            stack.Children.Add(showPass);

            var row = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var cancel = MakeBtn("Cancel", "#2a2a2a", "White");
            cancel.Click += (_, _) => DialogResult = false;
            var save = MakeBtn("Save", "#1f538d", "White");
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_userBox.Text) ||
                    string.IsNullOrWhiteSpace(_passBox.Password)) return;
                ResultUsername = _userBox.Text.Trim();
                ResultPassword = _passBox.Password;
                ResultNote     = _noteBox.Text.Trim();
                DialogResult   = true;
            };
            row.Children.Add(cancel);
            row.Children.Add(save);
            stack.Children.Add(row);
            Content = stack;
        }

        private static TextBox MakeTextBox(string text, string placeholder) => new()
        {
            Text        = text,
            Height      = 36, Margin = new Thickness(0, 0, 0, 8),
            Background  = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x2e)),
            Foreground  = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            Padding     = new Thickness(8, 6, 8, 6),
            FontFamily  = new FontFamily("Segoe UI"), FontSize = 13
        };

        private static Button MakeBtn(string text, string bg, string fg) => new()
        {
            Content    = text, Width = 130, Height = 36,
            Margin     = new Thickness(6, 0, 6, 0),
            Background = new SolidColorBrush((Color)new ColorConverter().ConvertFrom(bg)!),
            Foreground = new SolidColorBrush((Color)new ColorConverter().ConvertFrom(fg)!),
            FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            BorderThickness = new Thickness(0)
        };
    }
}
