using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SASC.Models;

namespace SASC.Services
{
    public class DiscordService
    {
        private static readonly string RoamingDiscord = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord");

        private static readonly string LevelDbPath =
            Path.Combine(RoamingDiscord, "Local Storage", "leveldb");

        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "account manager");

        private static readonly string ProfilesRoot = Path.Combine(DataDir, "DiscordProfiles");
        private static readonly string ProfilesJson = Path.Combine(DataDir, "discord_profiles.json");

        // ── Profile metadata ──────────────────────────────────────────────────

        public List<DiscordAccount> GetSavedProfiles()
        {
            if (!File.Exists(ProfilesJson)) return new();
            try
            {
                return JsonSerializer.Deserialize<List<DiscordAccount>>(
                    File.ReadAllText(ProfilesJson)) ?? new();
            }
            catch { return new(); }
        }

        private void SaveProfileList(List<DiscordAccount> profiles) =>
            File.WriteAllText(ProfilesJson,
                JsonSerializer.Serialize(profiles,
                    new JsonSerializerOptions { WriteIndented = true }));

        public void DeleteProfile(string username)
        {
            var profiles = GetSavedProfiles();
            profiles.RemoveAll(p => p.Username == username);
            SaveProfileList(profiles);
            var dir = GetSnapshotPath(username);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }

        public void SetActiveProfile(string username)
        {
            var profiles = GetSavedProfiles();
            foreach (var p in profiles) p.IsActive = p.Username == username;
            SaveProfileList(profiles);
        }

        // ── Save current Discord session as profile ───────────────────────────

        public void SaveCurrentAsProfile(string username, string note = "")
        {
            Directory.CreateDirectory(ProfilesRoot);
            var dest = GetSnapshotPath(username);
            if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
            Directory.CreateDirectory(dest);

            if (!Directory.Exists(LevelDbPath))
                throw new DirectoryNotFoundException(
                    "Discord LevelDB not found — is Discord installed?");

            int copied = 0;
            foreach (var file in Directory.GetFiles(LevelDbPath))
            {
                try
                {
                    File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
                    copied++;
                }
                catch { }
            }

            if (copied == 0)
                throw new Exception(
                    "Could not copy any files — make sure Discord is fully closed first.");

            var profiles = GetSavedProfiles();
            profiles.RemoveAll(p => p.Username == username);
            profiles.Add(new DiscordAccount
            {
                Username = username,
                DisplayName = username,
                Note = note,
                IsActive = false
            });
            SaveProfileList(profiles);
        }

        // ── Switch by folder swap ─────────────────────────────────────────────

        public async Task SwitchProfileAsync(string username, int killWaitMs = 2500)
        {
            var snapshot = GetSnapshotPath(username);
            if (!Directory.Exists(snapshot))
                throw new DirectoryNotFoundException(
                    $"No snapshot found for '{username}' — save the profile first.");

            await KillDiscordAsync(killWaitMs);

            foreach (var file in Directory.GetFiles(LevelDbPath))
                try { File.Delete(file); } catch { }

            foreach (var file in Directory.GetFiles(snapshot))
            {
                try
                {
                    File.Copy(file,
                        Path.Combine(LevelDbPath, Path.GetFileName(file)),
                        overwrite: true);
                }
                catch { }
            }

            LaunchDiscord();
            SetActiveProfile(username);
        }

        // ── Discord process ───────────────────────────────────────────────────

        public async Task KillDiscordAsync(int waitMs = 2500)
        {
            var procs = Process.GetProcessesByName("Discord")
                .Concat(Process.GetProcessesByName("DiscordPTB"))
                .Concat(Process.GetProcessesByName("DiscordCanary"))
                .ToArray();

            foreach (var p in procs)
                try { p.Kill(); } catch { }

            if (procs.Length > 0)
                await Task.Delay(waitMs);
        }

        public void LaunchDiscord()
        {
            var localDiscord = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Discord");

            if (!Directory.Exists(localDiscord)) return;

            var updateExe = Path.Combine(localDiscord, "Update.exe");
            if (File.Exists(updateExe))
            {
                Process.Start(new ProcessStartInfo(updateExe,
                    "--processStart Discord.exe")
                { UseShellExecute = true });
                return;
            }

            var discordExe = Directory
                .GetFiles(localDiscord, "Discord.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (discordExe != null)
                Process.Start(new ProcessStartInfo(discordExe)
                { UseShellExecute = true });
        }

        private static string GetSnapshotPath(string username) =>
            Path.Combine(ProfilesRoot, username);
    }
}
