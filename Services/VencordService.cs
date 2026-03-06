using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SASC.Services
{
    public class VencordService
    {
        private static readonly HttpClient _http = new();

        // Installer landet in Temp — ist nur ein Download-Cache, kein Datenverlust
        private static readonly string TempDir = Path.Combine(
            Path.GetTempPath(), "account manager");

        private static readonly string InstallerExe =
            Path.Combine(TempDir, "VencordInstallerCli.exe");

        private const string InstallerUrl =
            "https://github.com/Vendicated/VencordInstaller/releases/latest/download/VencordInstallerCli.exe";

        static VencordService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "SASC");
        }

        // ── Discord finden ────────────────────────────────────────────────────

        public static string? FindDiscordResources(string variant = "Discord")
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] variants = string.IsNullOrEmpty(variant)
                ? new[] { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" }
                : new[] { variant };

            foreach (var v in variants)
            {
                string discordPath = Path.Combine(local, v);
                if (!Directory.Exists(discordPath)) continue;

                var appDir = Directory.GetDirectories(discordPath, "app-*")
                    .OrderByDescending(d => d)
                    .FirstOrDefault();
                if (appDir == null) continue;

                string resources = Path.Combine(appDir, "resources");
                if (Directory.Exists(resources)) return resources;
            }
            return null;
        }

        public static string? FindDiscordExe(string variant = "Discord")
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] variants = string.IsNullOrEmpty(variant)
                ? new[] { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" }
                : new[] { variant };

            foreach (var v in variants)
            {
                string discordPath = Path.Combine(local, v);
                if (!Directory.Exists(discordPath)) continue;
                string updater = Path.Combine(discordPath, "Update.exe");
                if (File.Exists(updater)) return updater;
            }
            return null;
        }

        public static void LaunchDiscord(string variant = "Discord")
        {
            string? exe = FindDiscordExe(variant);
            if (exe == null) return;
            string processName = variant switch
            {
                "DiscordPTB" => "DiscordPTB.exe",
                "DiscordCanary" => "DiscordCanary.exe",
                "DiscordDevelopment" => "DiscordDevelopment.exe",
                _ => "Discord.exe"
            };
            Process.Start(new ProcessStartInfo(exe, $"--processStart {processName}")
            {
                UseShellExecute = true
            });
        }

        public static bool IsInstalled(string variant = "Discord")
        {
            var res = FindDiscordResources(variant);
            if (res == null) return false;
            return File.Exists(Path.Combine(res, "app", "index.js"));
        }

        // ── Discord killen ────────────────────────────────────────────────────

        public static async Task KillDiscordAsync()
        {
            string[] names = { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" };
            foreach (var name in names)
                foreach (var proc in Process.GetProcessesByName(name))
                    try { proc.Kill(); } catch { }
            await Task.Delay(1500);
        }

        // ── Installer herunterladen ───────────────────────────────────────────

        private async Task EnsureInstallerAsync(IProgress<string> progress)
        {
            Directory.CreateDirectory(TempDir);
            progress.Report("vencord installer wird heruntergeladen...");
            byte[] bytes = await _http.GetByteArrayAsync(InstallerUrl);
            await File.WriteAllBytesAsync(InstallerExe, bytes);
        }

        private static string BranchArg(string variant) => variant switch
        {
            "DiscordPTB" => "ptb",
            "DiscordCanary" => "canary",
            "DiscordDevelopment" => "development",
            _ => "stable"
        };

        private static async Task RunInstallerAsync(string args)
        {
            var psi = new ProcessStartInfo(InstallerExe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi)!;
            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                string msg = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                throw new Exception($"exit {proc.ExitCode}: {msg.Trim()}");
            }
        }

        // ── Installieren / Reparieren ─────────────────────────────────────────

        public async Task InstallAsync(IProgress<string> progress, string variant = "Discord")
        {
            progress.Report("discord wird gesucht...");
            if (FindDiscordResources(variant) == null)
                throw new Exception($"Discord-Installation nicht gefunden ({variant}).");

            progress.Report("discord wird geschlossen...");
            await KillDiscordAsync();

            await EnsureInstallerAsync(progress);

            progress.Report("vencord wird installiert...");
            await RunInstallerAsync($"--install --branch {BranchArg(variant)}");

            progress.Report("installiert ✓");
        }

        // ── Deinstallieren ────────────────────────────────────────────────────

        public static async Task UninstallAsync(IProgress<string> progress, string variant = "Discord")
        {
            progress.Report("discord wird geschlossen...");
            await KillDiscordAsync();

            Directory.CreateDirectory(TempDir);
            progress.Report("vencord installer wird heruntergeladen...");
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "SASC");
            byte[] bytes = await http.GetByteArrayAsync(InstallerUrl);
            await File.WriteAllBytesAsync(InstallerExe, bytes);

            progress.Report("vencord wird deinstalliert...");
            await RunInstallerAsync($"--uninstall --branch {BranchArg(variant)}");

            progress.Report("deinstalliert ✓");
        }
    }
}
