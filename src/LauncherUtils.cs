using System;
using System.Diagnostics;
using System.IO;
using LauncherConfig;
using Newtonsoft.Json;

namespace CanaryLauncherUpdate
{
    internal static class LauncherUtils
    {
        public static string GetLauncherPath(ClientConfig config, bool onlyBaseDirectory = false)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (onlyBaseDirectory || string.IsNullOrEmpty(config.clientFolder))
                return baseDir;
            return Path.Combine(baseDir, config.clientFolder);
        }

        public static string GetClientVersion(string basePath)
        {
            string jsonPath = Path.Combine(basePath, "launcher_config.json");
            if (!File.Exists(jsonPath))
                return string.Empty;
            var content = File.ReadAllText(jsonPath);
            var cfg = JsonConvert.DeserializeObject<ClientConfig>(content);
            return cfg?.clientVersion ?? string.Empty;
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
