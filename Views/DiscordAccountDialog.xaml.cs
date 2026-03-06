using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SASC.Views
{
    public partial class DiscordAccountDialog : Window
    {
        public string ResultUsername { get; private set; } = "";
        public string ResultToken    { get; private set; } = "";
        public string ResultNote     { get; private set; } = "";

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

        public DiscordAccountDialog(string username = "", string token = "", string note = "")
        {
            InitializeComponent();
            TxtUsername.Text = username;
            TxtToken.Text    = token;
            TxtNote.Text     = note;
        }

        private void TitleBar_MouseLeftButtonDown(object s, MouseButtonEventArgs e) => DragMove();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUsername.Text))
            {
                TxtUsername.BorderBrush = System.Windows.Media.Brushes.Red;
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtToken.Text))
            {
                TxtToken.BorderBrush = System.Windows.Media.Brushes.Red;
                return;
            }
            ResultUsername = TxtUsername.Text.Trim();
            ResultToken    = TxtToken.Text.Trim();
            ResultNote     = TxtNote.Text.Trim();
            DialogResult   = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) =>
            DialogResult = false;
    }
}
