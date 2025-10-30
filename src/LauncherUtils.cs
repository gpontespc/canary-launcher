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
        public const string DefaultLauncherConfigUrl = "https://raw.githubusercontent.com/gpontespc/canary-launcher/main/launcher_config.json";

        public static string GetLauncherConfigUrl(ClientConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config?.newConfigUrl))
                return config.newConfigUrl;
            return DefaultLauncherConfigUrl;
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
