using System;
using System.Windows;
using System.IO;
using System.Net;
using System.Windows.Threading;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading.Tasks;
using LauncherConfig;

namespace CanaryLauncherUpdate
{
	public partial class SplashScreen : Window
	{
		static string launcerConfigUrl = "https://raw.githubusercontent.com/gpontespc/canary-launcher/main/launcher_config.json";
		// Load informations of launcher_config.json file
		static ClientConfig clientConfig = ClientConfig.loadFromFile(launcerConfigUrl);

		static string clientExecutableName = clientConfig.clientExecutable;
		static string urlClient = clientConfig.newClientUrl;

		static readonly HttpClient httpClient = new HttpClient();
		DispatcherTimer timer = new DispatcherTimer();


		public SplashScreen()
		{
			string newVersion = clientConfig.clientVersion;
			if (newVersion == null)
			{
				this.Close();
			}

			// Start the client if the versions are the same
                        if (File.Exists(LauncherUtils.GetLauncherPath(clientConfig, true) + "/launcher_config.json")) {
                                string actualVersion = LauncherUtils.GetClientVersion(LauncherUtils.GetLauncherPath(clientConfig, true));
                                if (newVersion == actualVersion && Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig)) ) {
                                        LauncherUtils.LaunchClient(Path.Combine(LauncherUtils.GetLauncherPath(clientConfig), "bin", clientExecutableName), clientConfig.clientPriority);
                                        this.Close();
                                }
                        }

			InitializeComponent();
			timer.Tick += new EventHandler(timer_SplashScreen);
			timer.Interval = new TimeSpan(0, 0, 5);
			timer.Start();
		}

		public async void timer_SplashScreen(object sender, EventArgs e)
		{
			var requestClient = new HttpRequestMessage(HttpMethod.Post, urlClient);
			var response = await httpClient.SendAsync(requestClient);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				this.Close();
			}

                        if (!Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig)))
                        {
                                Directory.CreateDirectory(LauncherUtils.GetLauncherPath(clientConfig));
                        }
			MainWindow mainWindow = new MainWindow();
			this.Close();
			mainWindow.Show();
			timer.Stop();
		}
	}
}
