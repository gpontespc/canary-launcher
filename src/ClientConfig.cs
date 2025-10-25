using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CanaryLauncherUpdate;

namespace LauncherConfig
{
	public class ClientConfig
	{
		public string clientVersion { get; set; }
		public string launcherVersion { get; set; }
		public bool replaceFolders { get; set; }
		public ReplaceFolderName[] replaceFolderName { get; set; }
		public string clientFolder { get; set; }
                public string newClientUrl { get; set; }
                public string newConfigUrl { get; set; }
                public string clientExecutable { get; set; }
                public string clientPriority { get; set; }

                public static async Task<ClientConfig> LoadFromUrlAsync(string url)
                {
                        string jsonString = await LauncherUtils.HttpClient.GetStringAsync(url).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<ClientConfig>(jsonString);
                }

                public static async Task<ClientConfig> LoadFromFileAsync(string path)
                {
                        string jsonString = await Task.Run(() => File.ReadAllText(path)).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<ClientConfig>(jsonString);
                }
	}

	public class ReplaceFolderName
	{
		public string name { get; set; }
	}
}
