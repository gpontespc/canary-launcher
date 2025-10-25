using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using LauncherConfig;
using Newtonsoft.Json;

namespace CanaryLauncherUpdate
{
    internal static class LauncherUtils
    {
        static readonly HttpClient httpClient = CreateHttpClient();

        public static HttpClient HttpClient => httpClient;

        public static string GetLauncherPath(ClientConfig config, bool onlyBaseDirectory = false)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (onlyBaseDirectory || string.IsNullOrEmpty(config.clientFolder))
                return baseDir;
            return Path.Combine(baseDir, config.clientFolder);
        }

        static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(30),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 8,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            };

            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(15)
            };

            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CanaryLauncher", "1.0"));
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            return client;
        }

        public static async Task DownloadFileAsync(string requestUri, string destinationPath, IProgress<double> progress = null, IProgress<DownloadBytesProgress> bytesProgress = null, CancellationToken cancellationToken = default)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    string directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    long? contentLength = response.Content.Headers.ContentLength;

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    using (ProgressStream progressStream = new ProgressStream(fileStream, progress, bytesProgress, contentLength))
                    {
                        await contentStream.CopyToAsync(progressStream, 81920, cancellationToken).ConfigureAwait(false);
                        await progressStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
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

        sealed class ProgressStream : Stream
        {
            readonly Stream innerStream;
            readonly IProgress<double> percentProgress;
            readonly IProgress<DownloadBytesProgress> bytesProgress;
            readonly long? totalBytes;
            long writtenBytes;

            internal ProgressStream(Stream innerStream, IProgress<double> percentProgress, IProgress<DownloadBytesProgress> bytesProgress, long? totalBytes)
            {
                this.innerStream = innerStream;
                this.percentProgress = percentProgress;
                this.bytesProgress = bytesProgress;
                this.totalBytes = totalBytes;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => innerStream.CanWrite;
            public override long Length => innerStream.Length;
            public override long Position { get => innerStream.Position; set => throw new NotSupportedException(); }

            public override void Flush()
            {
                innerStream.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return innerStream.FlushAsync(cancellationToken);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                innerStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                innerStream.Write(buffer, offset, count);
                UpdateProgress(count);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await innerStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                UpdateProgress(count);
            }

            void UpdateProgress(int written)
            {
                writtenBytes += written;
                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    percentProgress?.Report(writtenBytes * 100.0 / totalBytes.Value);
                }
                bytesProgress?.Report(new DownloadBytesProgress(writtenBytes, totalBytes));
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    innerStream.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        public readonly struct DownloadBytesProgress
        {
            public DownloadBytesProgress(long bytesReceived, long? totalBytes)
            {
                BytesReceived = bytesReceived;
                TotalBytes = totalBytes;
            }

            public long BytesReceived { get; }
            public long? TotalBytes { get; }
        }
    }
}
