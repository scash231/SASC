using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using SASC.Models;

namespace SASC.Services
{
    public class AppSettings
    {
        public string SteamPath { get; set; } = @"C:\Program Files (x86)\Steam";
        public string EpicPath { get; set; } = @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe";
        public int KillWait { get; set; } = 4;
        public bool MinimizeOnSwitch { get; set; } = false;
        public string CloseAction { get; set; } = "tray";
        public string LaunchArgs { get; set; } = "";
        public string AutoLogin { get; set; } = "";
        public string SortBy { get; set; } = "name";
        public bool DiscordAutoRestart { get; set; } = true;
        public string DiscordVariant { get; set; } = "Discord";
    }

    public class AccountService
    {
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "account manager");

        private static readonly string AccountsFile = Path.Combine(DataDir, "accounts.json");
        private static readonly string SettingsFile = Path.Combine(DataDir, "settings.json");

        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SASC";

        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

        public AccountService()
        {
            Directory.CreateDirectory(DataDir);
        }

        public Dictionary<string, SteamAccount> LoadManualAccounts()
        {
            if (!File.Exists(AccountsFile)) return new();
            var data = JsonSerializer.Deserialize<Dictionary<string, SteamAccount>>(
                File.ReadAllText(AccountsFile)) ?? new();
            foreach (var acc in data.Values)
                acc.Password = EncryptionService.Decrypt(acc.EncryptedPassword);
            return data;
        }

        public void SaveManualAccounts(Dictionary<string, SteamAccount> data)
        {
            Directory.CreateDirectory(DataDir);
            foreach (var acc in data.Values)
                acc.EncryptedPassword = EncryptionService.Encrypt(acc.Password);
            File.WriteAllText(AccountsFile, JsonSerializer.Serialize(data, Opts));
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFile)) return new();
            return JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(SettingsFile)) ?? new();
        }

        public void SaveSettings(AppSettings s)
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, Opts));
        }

        public void ExportAccounts(Dictionary<string, SteamAccount> data, string path) =>
            File.WriteAllText(path, JsonSerializer.Serialize(data, Opts));

        public Dictionary<string, SteamAccount> ImportAccounts(string path)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, SteamAccount>>(
                File.ReadAllText(path)) ?? new();
            foreach (var acc in data.Values)
                acc.Password = EncryptionService.Decrypt(acc.EncryptedPassword);
            return data;
        }

        public bool IsAutoStartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }

        public void SetAutoStart(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (enable)
            {
                string exe = System.Diagnostics.Process
                    .GetCurrentProcess().MainModule!.FileName;
                key?.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                key?.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
    }
}
