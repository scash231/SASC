using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using SASC.Models;

namespace SASC.Services
{
    public class SteamService
    {
        private readonly string _steamPath;
        private string VdfPath => Path.Combine(_steamPath, "config", "loginusers.vdf");
        private string SteamExe => Path.Combine(_steamPath, "steam.exe");

        public SteamService(string steamPath)
        {
            _steamPath = steamPath;
        }

        public List<SteamAccount> ParseAccounts()
        {
            var accounts = new List<SteamAccount>();
            if (!File.Exists(VdfPath)) return accounts;

            try
            {
                var text    = File.ReadAllText(VdfPath);
                var pattern = @"""(\d{17})""\s*\{([^}]*)\}";
                var matches = Regex.Matches(text, pattern, RegexOptions.Singleline);

                foreach (Match m in matches)
                {
                    var steamId = m.Groups[1].Value;
                    var block   = m.Groups[2].Value;

                    var acc = new SteamAccount
                    {
                        SteamId     = steamId,
                        Username    = ExtractVdfValue(block, "AccountName"),
                        PersonaName = ExtractVdfValue(block, "PersonaName"),
                        IsRecent    = ExtractVdfValue(block, "MostRecent") == "1"
                    };

                    if (!string.IsNullOrEmpty(acc.Username))
                        accounts.Add(acc);
                }
            }
            catch { }

            return accounts;
        }

        private static string ExtractVdfValue(string block, string key)
        {
            var match = Regex.Match(block, $@"""{key}""\s+""([^""]*)""");
            return match.Success ? match.Groups[1].Value : "";
        }

        public void PatchVdf(string username)
        {
            if (!File.Exists(VdfPath)) return;
            try
            {
                var text    = File.ReadAllText(VdfPath);
                // Set all MostRecent to 0
                text = Regex.Replace(text, @"""MostRecent""\s+""1""", @"""MostRecent""		""0""");
                // Find the block for this username and set MostRecent to 1
                var pattern = $@"(""AccountName""\s+(""{username}"".*?))""MostRecent""\s+""0""";
                text = Regex.Replace(text, pattern,
                    m => m.Value.Replace(@"""MostRecent""		""0""", @"""MostRecent""		""1"""),
                    RegexOptions.Singleline);
                File.WriteAllText(VdfPath, text);
            }
            catch { }
        }

        public void SetRegistry(string username)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Valve\Steam", writable: true);
                key?.SetValue("AutoLoginUser",    username);
                key?.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        public async Task KillSteamAsync(int waitSeconds)
        {
            foreach (var proc in Process.GetProcessesByName("steam"))
            {
                try { proc.Kill(); } catch { }
            }
            await Task.Delay(waitSeconds * 1000);
        }

        public void LaunchSteam(string extraArgs = "")
        {
            if (!File.Exists(SteamExe)) return;
            var args = string.IsNullOrWhiteSpace(extraArgs) ? "" : extraArgs;
            Process.Start(new ProcessStartInfo(SteamExe, args)
                { UseShellExecute = true });
        }

        public void LaunchSteamWithLogin(string username, string password, string extraArgs = "")
        {
            if (!File.Exists(SteamExe)) return;
            var args = $"-login {username} {password}";
            if (!string.IsNullOrWhiteSpace(extraArgs)) args += $" {extraArgs}";
            Process.Start(new ProcessStartInfo(SteamExe, args)
                { UseShellExecute = true });
        }
    }
}
