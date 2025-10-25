using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO.Compression;
using LauncherConfig;

namespace CanaryLauncherUpdate
{
        public partial class MainWindow : Window
        {
                const string launcherConfigUrl = "https://raw.githubusercontent.com/gpontespc/canary-launcher/main/launcher_config.json";

                ClientConfig clientConfig;

                string clientExecutableName;
                string urlClient;
                string programVersion;
                string newVersion = string.Empty;
                bool clientDownloaded = false;
                bool needUpdate = false;

                // folders that should never be overwritten once created
                static readonly HashSet<string> PreserveFolders = new HashSet<string>(
                        new[] { "conf", "characterdata" },
                        StringComparer.OrdinalIgnoreCase);

                public MainWindow(ClientConfig config)
                {
                        clientConfig = config;
                        InitializeComponent();
                }

                private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
                {
                        await InitializeAsync();
                }

                public async Task InitializeAsync()
                {
                        try
                        {
                                ClientConfig latestConfig = await ClientConfig.LoadFromUrlAsync(launcherConfigUrl);
                                if (latestConfig != null)
                                {
                                        clientConfig = latestConfig;
                                }
                        }
                        catch (Exception)
                        {
                        }

                        newVersion = clientConfig.clientVersion;
                        clientExecutableName = clientConfig.clientExecutable;
                        urlClient = clientConfig.newClientUrl;
                        programVersion = clientConfig.launcherVersion;

                        ImageLogoServer.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo.png"));
                        ImageLogoCompany.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo_company.png"));

                        progressbarDownload.Visibility = Visibility.Collapsed;
                        labelClientVersion.Visibility = Visibility.Collapsed;
                        labelDownloadPercent.Visibility = Visibility.Collapsed;

                        if (File.Exists(LauncherUtils.GetLauncherPath(clientConfig, true) + "/launcher_config.json"))
                        {
                                // Read actual client version
                                string actualVersion = LauncherUtils.GetClientVersion(LauncherUtils.GetLauncherPath(clientConfig, true));
                                labelVersion.Text = "v" + programVersion;

                                if (newVersion != actualVersion)
                                {
                                        buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
                                        buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
                                        labelClientVersion.Content = newVersion;
                                        labelClientVersion.Visibility = Visibility.Visible;
                                        buttonPlay.Visibility = Visibility.Visible;
                                        buttonPlay_tooltip.Text = "Update";
                                        needUpdate = true;
                                }
                        }
                        if (!File.Exists(LauncherUtils.GetLauncherPath(clientConfig, true) + "/launcher_config.json") ||
                            Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig)) &&
                            Directory.GetFiles(LauncherUtils.GetLauncherPath(clientConfig)).Length == 0 &&
                            Directory.GetDirectories(LauncherUtils.GetLauncherPath(clientConfig)).Length == 0)
                        {
                                labelVersion.Text = "v" + programVersion;
                                buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
                                buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
                                labelClientVersion.Content = "Download";
                                labelClientVersion.Visibility = Visibility.Visible;
                                buttonPlay.Visibility = Visibility.Visible;
                                buttonPlay_tooltip.Text = "Download";
                                needUpdate = true;
                        }

                        await Task.CompletedTask;
                }

                void CreateShortcut()
                {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string shortcutPath = Path.Combine(desktopPath, clientConfig.clientFolder + ".lnk");
			Type t = Type.GetTypeFromProgID("WScript.Shell");
			dynamic shell = Activator.CreateInstance(t);
			var lnk = shell.CreateShortcut(shortcutPath);
			try
			{
				lnk.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
				lnk.Description = clientConfig.clientFolder;
				lnk.Save();
			}
			finally
			{
				System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
			}
		}

                private void AddReadOnly()
                {
                        // If the files "eventschedule/boostedcreature/onlinenumbers" exist, set them as read-only
                        string eventSchedulePath = LauncherUtils.GetLauncherPath(clientConfig) + "/cache/eventschedule.json";
			if (File.Exists(eventSchedulePath)) {
				File.SetAttributes(eventSchedulePath, FileAttributes.ReadOnly);
			}
                        string boostedCreaturePath = LauncherUtils.GetLauncherPath(clientConfig) + "/cache/boostedcreature.json";
			if (File.Exists(boostedCreaturePath)) {
				File.SetAttributes(boostedCreaturePath, FileAttributes.ReadOnly);
			}
                        string onlineNumbersPath = LauncherUtils.GetLauncherPath(clientConfig) + "/cache/onlinenumbers.json";
			if (File.Exists(onlineNumbersPath)) {
				File.SetAttributes(onlineNumbersPath, FileAttributes.ReadOnly);
			}
		}

                private async Task UpdateClientAsync()
                {
                        if (!Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig, true)))
                        {
                                Directory.CreateDirectory(LauncherUtils.GetLauncherPath(clientConfig));
                        }

                        labelDownloadPercent.Visibility = Visibility.Visible;
                        progressbarDownload.Visibility = Visibility.Visible;
                        labelClientVersion.Visibility = Visibility.Collapsed;
                        buttonPlay.Visibility = Visibility.Collapsed;
                        progressbarDownload.Value = 0;

                        string archivePath = LauncherUtils.GetLauncherPath(clientConfig) + "/tibia.zip";
                        long? totalBytes = null;

                        var percentProgress = new Progress<double>(value =>
                        {
                                double boundedValue = double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(100, value));
                                progressbarDownload.Value = boundedValue;
                                if (boundedValue >= 100 && totalBytes.HasValue)
                                {
                                        labelDownloadPercent.Content = "Finishing, wait...";
                                }
                        });

                        var bytesProgress = new Progress<LauncherUtils.DownloadBytesProgress>(info =>
                        {
                                totalBytes = info.TotalBytes;
                                if (info.TotalBytes.HasValue)
                                {
                                        labelDownloadPercent.Content = SizeSuffix(info.BytesReceived) + " / " + SizeSuffix(info.TotalBytes.Value);
                                }
                                else
                                {
                                        labelDownloadPercent.Content = SizeSuffix(info.BytesReceived);
                                }
                        });

                        try
                        {
                                await LauncherUtils.DownloadFileAsync(urlClient, archivePath, percentProgress, bytesProgress);
                        }
                        catch (Exception ex)
                        {
                                labelVersion.Text = ex.ToString();
                                progressbarDownload.Visibility = Visibility.Collapsed;
                                labelDownloadPercent.Visibility = Visibility.Collapsed;
                                buttonPlay.Visibility = Visibility.Visible;
                                return;
                        }

                        try
                        {
                                await HandleClientDownloadAsync(archivePath);
                        }
                        catch (Exception ex)
                        {
                                labelVersion.Text = ex.ToString();
                                progressbarDownload.Visibility = Visibility.Collapsed;
                                labelDownloadPercent.Visibility = Visibility.Collapsed;
                                buttonPlay.Visibility = Visibility.Visible;
                        }
                }

        private async void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
                if (needUpdate == true || !Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig)))
                {
                        try
                        {
                                await UpdateClientAsync();
                        }
                        catch (Exception ex)
                        {
                                labelVersion.Text = ex.ToString();
                        }
                }
                else
                {
                        if (clientDownloaded == true || !Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig, true)))
                        {
                                LauncherUtils.LaunchClient(Path.Combine(LauncherUtils.GetLauncherPath(clientConfig), "bin", clientExecutableName), clientConfig.clientPriority);
                                this.Close();
                        }
                                else
                                {
                                        try
                                        {
                                                await UpdateClientAsync();
                                        }
                                        catch (Exception ex)
                                        {
                                                labelVersion.Text = ex.ToString();
                                        }
                                }
                        }
                }

        private void ExtractZip(string path, IProgress<int> progress)
        {
                // track which protected folders already existed before extraction
                var alreadyPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var folder in PreserveFolders)
                {
                        if (Directory.Exists(Path.Combine(LauncherUtils.GetLauncherPath(clientConfig), folder)))
                                alreadyPresent.Add(folder);
                }

                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                        long totalBytes = archive.Entries.Sum(e => e.Length);
                        long processedBytes = 0;

                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                                string[] parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                                // skip entries from protected folders that already exist
                                if (parts.Length > 0 && alreadyPresent.Contains(parts[0]))
                                {
                                        processedBytes += entry.Length;
                                        progress?.Report((int)(processedBytes * 100.0 / totalBytes));
                                        continue;
                                }

                                string destination = Path.Combine(LauncherUtils.GetLauncherPath(clientConfig), entry.FullName);
                                var directory = Path.GetDirectoryName(destination);
                                if (!string.IsNullOrEmpty(directory))
                                {
                                        Directory.CreateDirectory(directory);
                                }

                                if (!string.IsNullOrEmpty(entry.Name))
                                {
                                        // Use built-in extraction method which can be faster than manual streaming
                                        entry.ExtractToFile(destination, true);
                                        processedBytes += entry.Length;
                                        progress?.Report((int)(processedBytes * 100.0 / totalBytes));
                                }
                                else
                                {
                                        // directory entry
                                        Directory.CreateDirectory(destination);
                                }
                        }
                }
        }

        private async Task HandleClientDownloadAsync(string archivePath)
        {
                        buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
                        buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_play.png"));

                        if (clientConfig.replaceFolders)
                        {
                                foreach (ReplaceFolderName folderName in clientConfig.replaceFolderName)
                                {
                                        string folderPath = Path.Combine(LauncherUtils.GetLauncherPath(clientConfig), folderName.name);
                                        if (Directory.Exists(folderPath))
                                        {
                                                Directory.Delete(folderPath, true);
                                        }
                                }
                        }

                        progressbarDownload.Value = 0;
                        labelDownloadPercent.Content = "Extracting 0%";
                        var progress = new Progress<int>(value =>
                        {
                                progressbarDownload.Value = value;
                                labelDownloadPercent.Content = $"Extracting {value}%";
                        });

                        try
                        {
                                await Task.Run(() =>
                                {
                                        Directory.CreateDirectory(LauncherUtils.GetLauncherPath(clientConfig));
                                        ExtractZip(archivePath, progress);
                                });
                        }
                        finally
                        {
                                if (File.Exists(archivePath))
                                {
                                        try
                                        {
                                                File.Delete(archivePath);
                                        }
                                        catch (Exception)
                                        {
                                        }
                                }
                        }

                        string localPath = Path.Combine(LauncherUtils.GetLauncherPath(clientConfig, true), "launcher_config.json");

                        try
                        {
                                await LauncherUtils.DownloadFileAsync(launcherConfigUrl, localPath);
                        }
                        catch (Exception)
                        {
                        }

                        if (File.Exists(localPath))
                        {
                                try
                                {
                                        clientConfig = await ClientConfig.LoadFromFileAsync(localPath);
                                        newVersion = clientConfig.clientVersion;
                                        clientExecutableName = clientConfig.clientExecutable;
                                        urlClient = clientConfig.newClientUrl;
                                        programVersion = clientConfig.launcherVersion;
                                        labelVersion.Text = "v" + programVersion;
                                }
                                catch (Exception)
                                {
                                }
                        }

                        AddReadOnly();
                        CreateShortcut();

                        needUpdate = false;
                        clientDownloaded = true;
                        string installedVersion = LauncherUtils.GetClientVersion(LauncherUtils.GetLauncherPath(clientConfig, true));
                        labelClientVersion.Content = installedVersion;
                        buttonPlay_tooltip.Text = installedVersion;
                        labelClientVersion.Visibility = Visibility.Visible;
                        buttonPlay.Visibility = Visibility.Visible;
                        progressbarDownload.Visibility = Visibility.Collapsed;
                        labelDownloadPercent.Visibility = Visibility.Collapsed;
        }

		static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
		static string SizeSuffix(Int64 value, int decimalPlaces = 1)
		{
			if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
			if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
			if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

			int mag = (int)Math.Log(value, 1024);
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));

			if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
			{
				mag += 1;
				adjustedSize /= 1024;
			}
			return string.Format("{0:n" + decimalPlaces + "} {1}",
				adjustedSize,
				SizeSuffixes[mag]);
		}

		private void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
		{
                        if (File.Exists(LauncherUtils.GetLauncherPath(clientConfig) + "/launcher_config.json"))
                        {
                                string actualVersion = LauncherUtils.GetClientVersion(LauncherUtils.GetLauncherPath(clientConfig, true));
				if (newVersion != actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_update.png")));
				}
				if (newVersion == actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_play.png")));
				}
			}
			else
			{
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_update.png")));
			}
		}

		private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
		{
                        if (File.Exists(LauncherUtils.GetLauncherPath(clientConfig, true) + "/launcher_config.json"))
                        {
                                string actualVersion = LauncherUtils.GetClientVersion(LauncherUtils.GetLauncherPath(clientConfig, true));
				if (newVersion != actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
				}
				if (newVersion == actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
				}
			}
			else
			{
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			if (ResizeMode != ResizeMode.NoResize)
			{
				if (WindowState == WindowState.Normal)
					WindowState = WindowState.Maximized;
				else
					WindowState = WindowState.Normal;
			}
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

	}
}
