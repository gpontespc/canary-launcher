using System;

namespace CanaryLauncherUpdate
{
  internal sealed class DownloadProgressInfo
  {
    public DownloadProgressInfo(long bytesReceived, long? totalBytes, double? percentage)
    {
      BytesReceived = bytesReceived;
      TotalBytes = totalBytes;
      Percentage = percentage;
    }

    public long BytesReceived { get; }

    public long? TotalBytes { get; }

    public double? Percentage { get; }
  }
}
