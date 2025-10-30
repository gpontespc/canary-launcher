using System;
using System.Collections.Generic;
using System.IO;

namespace CanaryLauncherUpdate
{
  internal sealed class ClientVersionStore
  {
    readonly Func<string> basePathProvider;

    public ClientVersionStore(Func<string> basePathProvider)
    {
      this.basePathProvider = basePathProvider ?? throw new ArgumentNullException(nameof(basePathProvider));
    }

    string VersionFilePath => Path.Combine(basePathProvider(), "client_version.txt");

    public ClientVersionInfo Load()
    {
      try
      {
        string path = VersionFilePath;
        if (!File.Exists(path))
          return null;
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0)
          return null;

        string versionRaw = null;
        string versionNormalized = null;
        string assetsSignature = null;

        string TrimLine(int index)
        {
          return index >= 0 && index < lines.Length ? lines[index]?.Trim() : null;
        }

        versionNormalized = TrimLine(0);
        if (lines.Length == 1)
        {
          if (string.IsNullOrEmpty(versionNormalized))
            return null;
        }
        else
        {
          string first = TrimLine(0);
          string second = TrimLine(1);
          bool looksLikeNewFormat = false;

          if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second))
          {
            try
            {
              string normalized = LauncherUtils.NormalizeVersion(first);
              looksLikeNewFormat = string.Equals(normalized, second, StringComparison.Ordinal);
            }
            catch
            {
              looksLikeNewFormat = false;
            }
          }

          if (looksLikeNewFormat)
          {
            versionRaw = first;
            versionNormalized = second;
            assetsSignature = TrimLine(2);
          }
          else
          {
            versionNormalized = first;
            assetsSignature = second;
            if (lines.Length > 2)
            {
              string extra = TrimLine(2);
              if (!string.IsNullOrEmpty(extra))
                assetsSignature = extra;
            }
          }
        }

        if (string.IsNullOrEmpty(versionNormalized))
          return null;

        assetsSignature = string.IsNullOrEmpty(assetsSignature) ? null : assetsSignature;
        return new ClientVersionInfo(versionRaw, versionNormalized, assetsSignature);
      }
      catch (IOException)
      {
        return null;
      }
      catch (UnauthorizedAccessException)
      {
        return null;
      }
    }

    public void Save(string versionRaw, string versionNormalized, string assetsSignature)
    {
      if (string.IsNullOrWhiteSpace(versionNormalized))
        throw new ArgumentException("Version cannot be empty", nameof(versionNormalized));

      string path = VersionFilePath;
      string directory = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        Directory.CreateDirectory(directory);

      var lines = new List<string>();
      if (!string.IsNullOrWhiteSpace(versionRaw))
      {
        lines.Add(versionRaw);
        lines.Add(versionNormalized);
      }
      else
      {
        lines.Add(versionNormalized);
      }

      if (!string.IsNullOrEmpty(assetsSignature))
        lines.Add(assetsSignature);

      File.WriteAllLines(path, lines);
    }
  }

  internal sealed class ClientVersionInfo
  {
    public ClientVersionInfo(string versionRaw, string versionNormalized, string assetsSignature)
    {
      VersionRaw = versionRaw;
      VersionNormalized = versionNormalized;
      AssetsSignature = assetsSignature;
    }

    public string VersionRaw { get; }

    public string VersionNormalized { get; }

    public string AssetsSignature { get; }
  }
}
