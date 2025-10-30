using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using LauncherConfig;
using Newtonsoft.Json;

namespace CanaryLauncherUpdate
{
    internal static class LauncherUtils
    {
        internal readonly struct VersionComponents
        {
            public VersionComponents(string baseVersion, string timestamp)
            {
                BaseVersion = baseVersion ?? string.Empty;
                Timestamp = timestamp ?? string.Empty;
            }

            public string BaseVersion { get; }

            public string Timestamp { get; }

            public bool HasBaseVersion => !string.IsNullOrEmpty(BaseVersion);

            public bool HasTimestamp => !string.IsNullOrEmpty(Timestamp);
        }

        public const string DefaultLauncherConfigUrl = "https://raw.githubusercontent.com/gpontespc/canary-launcher/main/launcher_config.json";
        public const string DefaultLauncherConfigFallbackUrl = "https://axtbvbltppuw.objectstorage.sa-vinhedo-1.oci.customer-oci.com/n/axtbvbltppuw/b/bucket-client/o/launcher_config.json";

        public static string GetLauncherConfigUrl(ClientConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config?.newConfigUrl))
                return config.newConfigUrl;
            return DefaultLauncherConfigUrl;
        }

        public static bool TryGetFallbackLauncherConfigUrl(string currentUrl, out string fallbackUrl)
        {
            if (!string.IsNullOrWhiteSpace(currentUrl) && string.Equals(currentUrl, DefaultLauncherConfigUrl, StringComparison.OrdinalIgnoreCase))
            {
                fallbackUrl = DefaultLauncherConfigFallbackUrl;
                return true;
            }

            fallbackUrl = null;
            return false;
        }

        public static string GetLauncherPath(ClientConfig config, bool onlyBaseDirectory = false)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (onlyBaseDirectory || string.IsNullOrEmpty(config.clientFolder))
                return baseDir;
            return Path.Combine(baseDir, config.clientFolder);
        }

        public static string GetClientVersion(string basePath)
        {
            string versionFilePath = Path.Combine(basePath, "client_version.txt");
            if (File.Exists(versionFilePath))
            {
                try
                {
                    string version = File.ReadLines(versionFilePath).FirstOrDefault();
                    if (!string.IsNullOrEmpty(version))
                        return version;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            string jsonPath = Path.Combine(basePath, "launcher_config.json");
            if (!File.Exists(jsonPath))
                return string.Empty;
            var content = File.ReadAllText(jsonPath);
            var cfg = JsonConvert.DeserializeObject<ClientConfig>(content);
            return cfg?.clientVersion ?? string.Empty;
        }

        public static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return string.Empty;

            StringBuilder builder = new StringBuilder(version.Length);
            foreach (char ch in version)
            {
                if (char.IsDigit(ch))
                    builder.Append(ch);
            }
            return builder.Length > 0 ? builder.ToString() : string.Empty;
        }

        public static VersionComponents SplitVersionComponents(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new VersionComponents(string.Empty, string.Empty);

            string[] parts = version
                .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            string major = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            string minor = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            string timestamp = parts.Length > 2
                ? string.Join(string.Empty, parts.Skip(2).Select(p => p.Trim()))
                : string.Empty;

            string baseVersion = string.Empty;
            if (!string.IsNullOrEmpty(major))
            {
                baseVersion = !string.IsNullOrEmpty(minor)
                    ? string.Concat(major, "/", minor)
                    : major;
            }
            else if (!string.IsNullOrEmpty(minor))
            {
                baseVersion = minor;
            }

            return new VersionComponents(baseVersion, timestamp);
        }

        public static int CompareNormalizedVersions(string left, string right)
        {
            BigInteger leftValue = ParseVersion(left);
            BigInteger rightValue = ParseVersion(right);
            return leftValue.CompareTo(rightValue);
        }

        static BigInteger ParseVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return BigInteger.Zero;

            BigInteger value = BigInteger.Zero;
            foreach (char ch in version)
            {
                if (!char.IsDigit(ch))
                    continue;
                value = value * 10 + (ch - '0');
            }
            return value;
        }

        public static Process LaunchClient(string exePath, string priorityName)
        {
            if (!File.Exists(exePath))
                return null;
            var process = Process.Start(exePath);
            SetPriority(process, priorityName);
            return process;
        }

        public static void SetPriority(Process process, string priorityName)
        {
            if (process == null)
                return;
            try
            {
                ProcessPriorityClass priority = ProcessPriorityClass.Normal;
                if (!string.IsNullOrEmpty(priorityName))
                    Enum.TryParse(priorityName, true, out priority);
                process.PriorityClass = priority;
            }
            catch { }
        }
    }
}
