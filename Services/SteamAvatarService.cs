using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SASC.Services
{
    public class SteamAvatarService
    {
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "account manager", "avatarcache");

        private static readonly HttpClient Http = new();

        public SteamAvatarService()
        {
            Directory.CreateDirectory(CacheDir);
        }

        public string? GetCachedPath(string steamId)
        {
            var path = Path.Combine(CacheDir, $"{steamId}.jpg");
            return File.Exists(path) ? path : null;
        }

        public async Task FetchAvatarsAsync(IEnumerable<string> steamIds)
        {
            var tasks = steamIds.Select(id => FetchSingleAsync(id));
            await Task.WhenAll(tasks);
        }

        private async Task FetchSingleAsync(string steamId)
        {
            var destPath = Path.Combine(CacheDir, $"{steamId}.jpg");
            if (File.Exists(destPath)) return;

            try
            {
                var xml = await Http.GetStringAsync(
                    $"https://steamcommunity.com/profiles/{steamId}/?xml=1");
                var doc = XDocument.Parse(xml);
                var avatarUrl = doc.Root?.Element("avatarMedium")?.Value;
                if (string.IsNullOrEmpty(avatarUrl)) return;
                var bytes = await Http.GetByteArrayAsync(avatarUrl);
                await File.WriteAllBytesAsync(destPath, bytes);
            }
            catch { }
        }

        public async Task RefreshAvatarAsync(string steamId)
        {
            var path = Path.Combine(CacheDir, $"{steamId}.jpg");
            if (File.Exists(path)) File.Delete(path);
            await FetchSingleAsync(steamId);
        }
    }
}
