using System;
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
        string version = lines[0].Trim();
        string assetsSignature = lines.Length > 1 ? lines[1].Trim() : null;
        return new ClientVersionInfo(version, string.IsNullOrEmpty(assetsSignature) ? null : assetsSignature);
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

    public void Save(string versionNormalized, string assetsSignature)
    {
      if (string.IsNullOrWhiteSpace(versionNormalized))
        throw new ArgumentException("Version cannot be empty", nameof(versionNormalized));

      string path = VersionFilePath;
      string directory = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        Directory.CreateDirectory(directory);

      string[] lines = string.IsNullOrEmpty(assetsSignature)
        ? new[] { versionNormalized }
        : new[] { versionNormalized, assetsSignature };
      File.WriteAllLines(path, lines);
    }
  }

  internal sealed class ClientVersionInfo
  {
    public ClientVersionInfo(string versionNormalized, string assetsSignature)
    {
      VersionNormalized = versionNormalized;
      AssetsSignature = assetsSignature;
    }

    public string VersionNormalized { get; }

    public string AssetsSignature { get; }
  }
}
