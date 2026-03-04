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
        private readonly string _steamExe;
        private readonly string _vdfPath;

        public SteamService(string steamPath)
        {
            _steamPath = steamPath;
            _steamExe  = Path.Combine(steamPath, "steam.exe");
            _vdfPath   = Path.Combine(steamPath, "config", "loginusers.vdf");
        }

        public List<SteamAccount> ParseAccounts()
        {
            var accounts = new List<SteamAccount>();
            if (!File.Exists(_vdfPath)) return accounts;

            string content = File.ReadAllText(_vdfPath);
            var blocks = Regex.Matches(content,
                @"""(\d{17})""\s*\{([^}]*)\}", RegexOptions.Singleline);

            foreach (Match block in blocks)
            {
                string steamId = block.Groups[1].Value;
                string body    = block.Groups[2].Value;
                string name    = GetVal(body, "AccountName");
                if (string.IsNullOrEmpty(name)) continue;

                accounts.Add(new SteamAccount
                {
                    Username         = name,
                    PersonaName      = GetVal(body, "PersonaName") is { Length: > 0 } p ? p : name,
                    SteamId          = steamId,
                    RememberPassword = GetVal(body, "RememberPassword") == "1",
                    IsRecent         = GetVal(body, "MostRecent") == "1",
                    IsManual         = false
                });
            }
            return accounts;
        }

        private static string GetVal(string block, string key)
        {
            var m = Regex.Match(block, $@"""{key}""\s+""([^""]*)""");
            return m.Success ? m.Groups[1].Value : "";
        }

        public void PatchVdf(string targetUsername)
        {
            if (!File.Exists(_vdfPath)) return;
            var lines  = File.ReadAllLines(_vdfPath);
            var result = new List<string>();
            bool inside = false;

            foreach (var line in lines)
            {
                var edited = line;
                var nm = Regex.Match(line, @"""AccountName""\s+""([^""]+)""");
                if (nm.Success)
                    inside = nm.Groups[1].Value.Equals(
                        targetUsername, System.StringComparison.OrdinalIgnoreCase);

                if (Regex.IsMatch(line, @"""MostRecent"""))
                    edited = Regex.Replace(line,
                        @"""MostRecent""\s+""[01]""",
                        $"\"MostRecent\"\t\t\"{(inside ? "1" : "0")}\"");

                if (inside && Regex.IsMatch(line, @"""RememberPassword"""))
                    edited = Regex.Replace(line,
                        @"""RememberPassword""\s+""[01]""",
                        "\"RememberPassword\"\t\t\"1\"");

                if (Regex.IsMatch(line, @"^\s*\}\s*$")) inside = false;
                result.Add(edited);
            }
            File.WriteAllLines(_vdfPath, result);
        }

        public void SetRegistry(string username)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Valve\Steam", writable: true);
            key?.SetValue("AutoLoginUser",    username);
            key?.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
        }

        public async Task KillSteamAsync(int waitSeconds)
        {
            foreach (var p in Process.GetProcessesByName("steam"))
            {
                p.Kill();
                await p.WaitForExitAsync();
            }
            await Task.Delay(waitSeconds * 1000);
        }

        public void LaunchSteam(string? args = null)
        {
            var info = new ProcessStartInfo(_steamExe);
            if (!string.IsNullOrEmpty(args)) info.Arguments = args;
            Process.Start(info);
        }

        public void LaunchSteamWithLogin(string user, string pass, string? extra = null)
        {
            string args = $"-login {user} {pass}";
            if (!string.IsNullOrEmpty(extra)) args += " " + extra;
            Process.Start(new ProcessStartInfo(_steamExe) { Arguments = args });
        }

        public bool SteamExeExists() => File.Exists(_steamExe);
        public bool VdfExists()      => File.Exists(_vdfPath);
    }
}
