using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CanaryLauncherUpdate
{
  internal sealed class DownloadManager : IDisposable
  {
    readonly HttpClient httpClient;

    public DownloadManager()
    {
      httpClient = new HttpClient();
    }

    public async Task DownloadFileAsync(Uri uri, string destinationPath, IProgress<DownloadProgressInfo> progress, CancellationToken cancellationToken)
    {
      if (uri == null)
        throw new ArgumentNullException(nameof(uri));
      if (string.IsNullOrEmpty(destinationPath))
        throw new ArgumentNullException(nameof(destinationPath));

      string tempPath = destinationPath + ".part";
      Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? AppDomain.CurrentDomain.BaseDirectory);

      long existingLength = 0;
      if (File.Exists(tempPath))
        existingLength = new FileInfo(tempPath).Length;

      HttpResponseMessage response = await SendDownloadRequestAsync(uri, existingLength, cancellationToken).ConfigureAwait(false);
      if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
      {
        response.Dispose();
        if (File.Exists(tempPath))
          File.Delete(tempPath);
        existingLength = 0;
        response = await SendDownloadRequestAsync(uri, 0, cancellationToken).ConfigureAwait(false);
      }
      else if (existingLength > 0 && response.StatusCode == HttpStatusCode.OK)
      {
        response.Dispose();
        existingLength = 0;
        response = await SendDownloadRequestAsync(uri, 0, cancellationToken).ConfigureAwait(false);
      }

      response.EnsureSuccessStatusCode();
      long? totalFromServer = response.Content.Headers.ContentLength;
      long? totalBytes = totalFromServer.HasValue ? totalFromServer + existingLength : (long?)null;
      long downloaded = existingLength;

      using (response)
      using (Stream networkStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
      using (FileStream fileStream = new FileStream(tempPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 81920, FileOptions.Asynchronous))
      {
        if (existingLength > 0)
          fileStream.Seek(existingLength, SeekOrigin.Begin);
        else
          fileStream.SetLength(0);

        byte[] buffer = new byte[81920];
        int read;
        while ((read = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
          await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
          downloaded += read;
          double? percentage = null;
          if (totalBytes.HasValue && totalBytes.Value > 0)
            percentage = downloaded * 100.0 / totalBytes.Value;
          progress?.Report(new DownloadProgressInfo(downloaded, totalBytes, percentage));
        }
      }

      if (File.Exists(destinationPath))
        File.Delete(destinationPath);
      File.Move(tempPath, destinationPath);
    }

    public async Task<string> FetchRemoteSignatureAsync(Uri uri, CancellationToken cancellationToken)
    {
      if (uri == null)
        return null;
      try
      {
        using HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
          if (response.StatusCode == HttpStatusCode.MethodNotAllowed || response.StatusCode == HttpStatusCode.NotFound)
            return null;
          return await TryExtractSignatureWithRangeAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        return ExtractSignature(response);
      }
      catch (HttpRequestException)
      {
        return null;
      }
      catch (TaskCanceledException)
      {
        return null;
      }
    }

    public async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
      using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
      using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
      httpClient.Dispose();
    }

    async Task<HttpResponseMessage> SendDownloadRequestAsync(Uri uri, long rangeStart, CancellationToken cancellationToken)
    {
      using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
      if (rangeStart > 0)
        request.Headers.Range = new RangeHeaderValue(rangeStart, null);
      return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    async Task<string> TryExtractSignatureWithRangeAsync(Uri uri, CancellationToken cancellationToken)
    {
      try
      {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
          return null;
        return ExtractSignature(response);
      }
      catch (HttpRequestException)
      {
        return null;
      }
      catch (TaskCanceledException)
      {
        return null;
      }
    }

    static string ExtractSignature(HttpResponseMessage response)
    {
      string etag = response.Headers.ETag?.Tag;
      if (!string.IsNullOrEmpty(etag))
        return etag;
      string lastModified = GetHeaderValue(response.Content?.Headers, "Last-Modified") ?? GetHeaderValue(response.Headers, "Last-Modified");
      long? contentLength = response.Content?.Headers?.ContentLength ?? TryGetContentLength(response.Headers);
      if (!string.IsNullOrEmpty(lastModified) || contentLength.HasValue)
        return $"{contentLength?.ToString() ?? "null"}:{lastModified}";
      return null;
    }

    static string GetHeaderValue(HttpHeaders headers, string name)
    {
      if (headers == null)
        return null;
      if (!headers.TryGetValues(name, out IEnumerable<string> values))
        return null;
      foreach (string value in values)
      {
        if (!string.IsNullOrEmpty(value))
          return value;
      }
      return null;
    }

    static long? TryGetContentLength(HttpHeaders headers)
    {
      if (headers == null)
        return null;
      string value = GetHeaderValue(headers, "Content-Length");
      if (long.TryParse(value, out long parsed))
        return parsed;
      return null;
    }
  }
}
