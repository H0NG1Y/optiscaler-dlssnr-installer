using System;
using System.Collections.Generic;

namespace OptiScalerInstaller
{
    public sealed class InstallOptions
    {
        public string GameExe;
        public string ModelDll;
        public string ProxyName = "dxgi.dll";
        public string Gpu = "NVIDIA";
        public bool EnableNeuralRendering = true;
        public bool UseDlssInputs = true;
        public string FsrDirectory;
        public bool InstallFsrEnabler;
        public bool InstallFsrNukem;
        public string StreamlineDirectory;
        public bool InstallDlssg;
        public ReleaseInfo StreamlineSdkRelease;
        public string BridgeDllFallback;
    }

    public sealed class UpstreamSource
    {
        public string Id;
        public string DisplayName;
        public string Owner;
        public string RepoName;
        public string ApiRoot { get { return "https://api.github.com/repos/" + Owner + "/" + RepoName + "/"; } }
        public string AssetPath { get { return "/" + Owner + "/" + RepoName + "/releases/download/"; } }
        public string ReleasesPageUrl { get { return "https://github.com/" + Owner + "/" + RepoName + "/releases"; } }
        public override string ToString() { return DisplayName; }
    }

    public sealed class ReleaseInfo
    {
        public string Tag;
        public string AssetName;
        public string DownloadUrl;
        public string Sha256;
        public long Size;
        public bool IsLatest;
        public string SourceId;
        public string SourceDisplay;
        public string AssetPath;
        public override string ToString()
        {
            string source = String.IsNullOrEmpty(SourceDisplay) ? "" : " · " + SourceDisplay;
            return Tag + (IsLatest ? "（Latest）" : "") + " — " + AssetName + source;
        }
    }

    public sealed class DownloadProgress
    {
        public long Received;
        public long Total;
    }

    public sealed class InstallManifest
    {
        public int Schema = 1;
        public string Root;
        public string GameExe;
        public string Version;
        public string ProxyName;
        public string InstalledUtc;
        public List<ManagedFile> Files = new List<ManagedFile>();
        public List<string> CreatedDirectories = new List<string>();
    }

    public sealed class ManagedFile
    {
        public string Path;
        public bool HadOriginal;
        public string OriginalHash;
        public string BackupName;
        public string InstalledHash;
    }
}
