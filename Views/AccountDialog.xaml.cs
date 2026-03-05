using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SASC.Views
{
    public partial class AccountDialog : Window
    {
        public string ResultUsername { get; private set; } = "";
        public string ResultPassword { get; private set; } = "";
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

        public AccountDialog(string username = "", string password = "", string note = "")
        {
            InitializeComponent();
            TxtUsername.Text = username;
            TxtPassword.Text = password;
            TxtNote.Text     = note;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUsername.Text)) return;
            ResultUsername = TxtUsername.Text.Trim();
            ResultPassword = TxtPassword.Text.Trim();
            ResultNote     = TxtNote.Text.Trim();
            DialogResult   = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) =>
            DialogResult = false;
    }
}
