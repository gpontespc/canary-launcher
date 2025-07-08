using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace BalrogLauncher.Services
{
    public class UpdateService
    {
        private readonly HttpClient _client = new HttpClient();
        //TODO: insert version URL
        private const string VERSION_URL = "https://example.com/version.txt";
        //TODO: insert patch URL
        private const string PATCH_URL = "https://example.com/patch.zip";

        public async Task<bool> EnsureClientAsync(Action<int> progress)
        {
            try
            {
                var localVersion = GetLocalVersion();
                var remoteVersion = await _client.GetStringAsync(VERSION_URL);
                if (localVersion != remoteVersion.Trim())
                {
                    var data = await _client.GetByteArrayAsync(PATCH_URL);
                    var temp = Path.GetTempFileName();
                    await File.WriteAllBytesAsync(temp, data);
                    await ExtractZipAsync(temp, progress);
                    File.Delete(temp);
                    File.WriteAllText("version.txt", remoteVersion);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GetLocalVersion()
        {
            return File.Exists("version.txt") ? File.ReadAllText("version.txt") : string.Empty;
        }

        private async Task ExtractZipAsync(string path, Action<int> progress)
        {
            using var archive = ZipFile.OpenRead(path);
            long total = 0;
            foreach (var entry in archive.Entries) total += entry.Length;
            long processed = 0;
            foreach (var entry in archive.Entries)
            {
                var dest = Path.Combine(Environment.CurrentDirectory, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, true);
                processed += entry.Length;
                progress((int)(processed * 100 / total));
                await Task.Yield();
            }
        }
    }
}
