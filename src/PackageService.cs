using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace OptiScalerInstaller
{
    public static class PackageService
    {
        public static readonly UpstreamSource SourceDagherbou = new UpstreamSource {
            Id = "dagherbou",
            DisplayName = "Dagherbou · DLSSNR",
            Owner = "Dagherbou",
            RepoName = "OptiScaler_DLSSNR"
        };
        public static readonly UpstreamSource SourcePresrMultipass = new UpstreamSource {
            Id = "presr-multipass",
            DisplayName = "wilsjo2 · PreSR-Multipass",
            Owner = "wilsjo2",
            RepoName = "OptiScaler-DLSSNR-PreSR-Multipass"
        };
        // NVIDIA Streamline SDK — for OptiScaler's own FG runtime at OptiScaler\streamline\
        public static readonly UpstreamSource SourceStreamlineSdk = new UpstreamSource {
            Id = "nvidia-streamline",
            DisplayName = "NVIDIA-RTX · Streamline SDK",
            Owner = "NVIDIA-RTX",
            RepoName = "Streamline"
        };
        public static readonly UpstreamSource[] SupportedSources = { SourceDagherbou, SourcePresrMultipass };
        static readonly UpstreamSource[] DownloadableSources = { SourceDagherbou, SourcePresrMultipass, SourceStreamlineSdk };
        public const long MaxPackageBytes = 1024L * 1024L * 1024L;
        const int MaxJsonBytes = 8 * 1024 * 1024;

        public static bool IsStreamlineSdkAsset(string assetName)
        {
            if (String.IsNullOrWhiteSpace(assetName)) return false;
            string n = assetName.Trim();
            if (!n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return false;
            if (n.IndexOf("aarch64", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("arm64", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return n.StartsWith("streamline-sdk-v", StringComparison.OrdinalIgnoreCase);
        }

        public static Task<List<ReleaseInfo>> FetchStreamlineSdkReleasesAsync(CancellationToken token)
        {
            return FetchReleasesCoreAsync(SourceStreamlineSdk, token);
        }

        static UpstreamSource RequireDownloadableSource(UpstreamSource source)
        {
            if (source == null || String.IsNullOrWhiteSpace(source.AssetPath)) throw new ArgumentNullException("source");
            foreach (var item in DownloadableSources)
                if (String.Equals(item.Id, source.Id, StringComparison.Ordinal) &&
                    String.Equals(item.AssetPath, source.AssetPath, StringComparison.Ordinal) &&
                    String.Equals(item.ApiRoot, source.ApiRoot, StringComparison.Ordinal)) return item;
            throw new InvalidOperationException("不支持的上游仓库。");
        }

        static UpstreamSource RequireSource(UpstreamSource source)
        {
            if (source == null || String.IsNullOrWhiteSpace(source.AssetPath)) throw new ArgumentNullException("source");
            foreach (var item in SupportedSources)
                if (String.Equals(item.Id, source.Id, StringComparison.Ordinal) &&
                    String.Equals(item.AssetPath, source.AssetPath, StringComparison.Ordinal) &&
                    String.Equals(item.ApiRoot, source.ApiRoot, StringComparison.Ordinal)) return item;
            throw new InvalidOperationException("不支持的上游仓库。");
        }

        static HttpClient CreateClient()
        {
            // Keep Windows certificate verification and the system's proxy configuration.
            // csc-built Framework executables can inherit TLS 1.0 defaults and reject SystemDefault.
            // GitHub requires TLS 1.2; certificate and hostname verification remain enabled.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var client = new HttpClient(handler);
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OptiScaler-DLSSNR-Chinese-Installer/1.0");
            return client;
        }

        public static Task<List<ReleaseInfo>> FetchReleasesAsync(CancellationToken token)
        {
            return FetchReleasesAsync(SourceDagherbou, token);
        }

        public static async Task<List<ReleaseInfo>> FetchReleasesAsync(UpstreamSource source, CancellationToken token)
        {
            return await FetchReleasesCoreAsync(RequireSource(source), token).ConfigureAwait(false);
        }

        static async Task<List<ReleaseInfo>> FetchReleasesCoreAsync(UpstreamSource source, CancellationToken token)
        {
            source = RequireDownloadableSource(source);
            token.ThrowIfCancellationRequested();
            bool native = CurlTransport.IsAvailable;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var client = native ? null : CreateClient())
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(40));
                try
                {
                    string apiRoot = source.ApiRoot;
                    var latest = native ? ReadJsonNativeAsync(new Uri(apiRoot + "releases/latest"), timeout.Token) : ReadJsonAsync(client, new Uri(apiRoot + "releases/latest"), timeout.Token);
                    var releases = native ? ReadJsonNativeAsync(new Uri(apiRoot + "releases?per_page=30"), timeout.Token) : ReadJsonAsync(client, new Uri(apiRoot + "releases?per_page=30"), timeout.Token);
                    await Task.WhenAll(latest, releases).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return ParseReleases(source, latest.Result, releases.Result);
                }
                catch (OperationCanceledException e)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException("版本查询已取消。", e, token);
                    throw new TimeoutException("查询 GitHub 版本超时，请检查网络或系统代理后重试。", e);
                }
                catch (HttpRequestException e) { throw new InvalidOperationException("无法连接 GitHub，请检查网络或系统代理后重试。", e); }
                catch (InvalidDataException) { throw; }
                catch (IOException e) { throw new InvalidOperationException("读取 GitHub 版本信息失败，请稍后重试。", e); }
            }
        }

        static async Task<string> ReadJsonNativeAsync(Uri uri, CancellationToken token)
        {
            string temporary = Path.Combine(Path.GetTempPath(), "OptiScaler-release-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var response = await CurlTransport.FetchToFileAsync(uri, temporary, MaxJsonBytes, false, null, token).ConfigureAwait(false);
                if (IsRedirect(response.StatusCode)) throw new InvalidDataException("GitHub API 返回了意外重定向，已停止读取版本信息。");
                CheckStatus(response.StatusCode);
                token.ThrowIfCancellationRequested();
                if (!File.Exists(temporary) || new FileInfo(temporary).Length > MaxJsonBytes) throw new InvalidDataException("GitHub 版本信息大小无效。");
                return File.ReadAllText(temporary, Encoding.UTF8);
            }
            finally { DeleteTemporary(temporary); }
        }

        static async Task<string> ReadJsonAsync(HttpClient client, Uri uri, CancellationToken token)
        {
            using (var response = await SendAsync(client, uri, false, token).ConfigureAwait(false))
            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var data = new MemoryStream())
            {
                var buffer = new byte[16384];
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                {
                    if (data.Length + read > MaxJsonBytes) throw new InvalidDataException("GitHub 版本信息过大，已停止读取。");
                    data.Write(buffer, 0, read);
                }
                return Encoding.UTF8.GetString(data.ToArray());
            }
        }

        internal static List<ReleaseInfo> ParseReleases(string latestJson, string releasesJson)
        {
            return ParseReleases(SourceDagherbou, latestJson, releasesJson);
        }

        internal static List<ReleaseInfo> ParseReleases(UpstreamSource source, string latestJson, string releasesJson)
        {
            source = RequireDownloadableSource(source);
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaxJsonBytes, RecursionLimit = 64 };
            IDictionary<string, object> latest;
            object[] releases;
            try
            {
                latest = serializer.DeserializeObject(latestJson) as IDictionary<string, object>;
                releases = serializer.DeserializeObject(releasesJson) as object[];
            }
            catch (ArgumentException e) { throw new InvalidDataException("GitHub 返回的版本信息格式无效，请稍后重试。", e); }
            catch (InvalidOperationException e) { throw new InvalidDataException("GitHub 返回的版本信息格式无效，请稍后重试。", e); }
            if (latest == null || releases == null) throw new InvalidDataException("GitHub 返回的版本列表格式无效。");

            var result = new List<ReleaseInfo>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AddRelease(source, latest, true, result, seen);
            if (result.Count == 0) throw new InvalidDataException("官方 Latest 未提供带有效 SHA-256 的稳定版 ZIP，已停止自动选择版本。");
            string latestTag = result[0].Tag;
            foreach (var item in releases)
            {
                var release = item as IDictionary<string, object>;
                if (release != null) AddRelease(source, release, String.Equals(Text(release, "tag_name"), latestTag, StringComparison.Ordinal), result, seen);
            }
            // GitHub latest is authoritative; published_at and semver do not choose the default.
            result.Sort(delegate(ReleaseInfo left, ReleaseInfo right) { return right.IsLatest.CompareTo(left.IsLatest); });
            return result;
        }

        static void AddRelease(UpstreamSource source, IDictionary<string, object> objectSource, bool latest, List<ReleaseInfo> target, HashSet<string> seen)
        {
            object draft, prerelease, assets;
            if (!objectSource.TryGetValue("draft", out draft) || !(draft is bool) || (bool)draft ||
                !objectSource.TryGetValue("prerelease", out prerelease) || !(prerelease is bool) || (bool)prerelease ||
                !objectSource.TryGetValue("assets", out assets) || !(assets is object[])) return;
            foreach (var value in (object[])assets)
            {
                var asset = value as IDictionary<string, object>;
                if (asset == null) continue;
                object sizeValue;
                string digest = Text(asset, "digest");
                if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                    !asset.TryGetValue("size", out sizeValue) || !(sizeValue is int || sizeValue is long)) continue;
                var release = new ReleaseInfo {
                    Tag = Text(objectSource, "tag_name"), AssetName = Text(asset, "name"), DownloadUrl = Text(asset, "browser_download_url"),
                    Sha256 = digest.Substring(7).ToLowerInvariant(), Size = Convert.ToInt64(sizeValue, CultureInfo.InvariantCulture), IsLatest = latest,
                    SourceId = source.Id, SourceDisplay = source.DisplayName, AssetPath = source.AssetPath
                };
                try { ValidateRelease(release); }
                catch (InvalidDataException) { continue; }
                if (seen.Add(release.SourceId + "|" + release.DownloadUrl)) target.Add(release);
            }
        }

        static string Text(IDictionary<string, object> item, string key)
        {
            object value;
            return item.TryGetValue(key, out value) ? value as string : null;
        }

        static bool IsHttps(Uri uri)
        {
            return uri != null && uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 &&
                String.IsNullOrEmpty(uri.UserInfo) && String.IsNullOrEmpty(uri.Fragment);
        }

        static void ValidateRelease(ReleaseInfo release)
        {
            if (release == null || String.IsNullOrWhiteSpace(release.Tag) || String.IsNullOrWhiteSpace(release.AssetName) ||
                release.Tag.IndexOfAny(new[] { '\r', '\n', '\0', '\\' }) >= 0 ||
                release.AssetName.IndexOfAny(new[] { '/', '\\', '\r', '\n', '\0' }) >= 0 ||
                !release.AssetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("发行包名称无效，只接受官方 ZIP 资产。");
            if (release.Size <= 0 || release.Size > MaxPackageBytes) throw new InvalidDataException("发行包大小无效；仅接受大于 0 且不超过 1 GB 的 ZIP。");
            if (release.Sha256 == null || release.Sha256.Length != 64) throw new InvalidDataException("发行包缺少有效 SHA-256，无法验证下载完整性。");
            foreach (char c in release.Sha256)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    throw new InvalidDataException("发行包 SHA-256 格式无效。");
            Uri uri;
            if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out uri) || !IsHttps(uri) ||
                !String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) || !String.IsNullOrEmpty(uri.Query))
                throw new InvalidDataException("下载地址无效；仅允许已启用上游仓库的 HTTPS 发行资产。");
            string expectedTail = release.Tag + "/" + release.AssetName;
            string absolutePath = Uri.UnescapeDataString(uri.AbsolutePath);
            string assetPath = release.AssetPath;
            if (!String.IsNullOrEmpty(assetPath))
            {
                bool known = false;
                foreach (var source in DownloadableSources)
                    if (String.Equals(source.AssetPath, assetPath, StringComparison.Ordinal)) { known = true; break; }
                if (!known) throw new InvalidDataException("下载地址无效；仅允许已启用上游仓库的 HTTPS 发行资产。");
            }
            else if (!String.IsNullOrEmpty(release.SourceId))
            {
                foreach (var source in DownloadableSources)
                    if (String.Equals(source.Id, release.SourceId, StringComparison.Ordinal)) { assetPath = source.AssetPath; break; }
                if (String.IsNullOrEmpty(assetPath)) throw new InvalidDataException("下载地址无效；仅允许已启用上游仓库的 HTTPS 发行资产。");
            }
            else
            {
                foreach (var source in DownloadableSources)
                    if (String.Equals(absolutePath, source.AssetPath + expectedTail, StringComparison.Ordinal)) { assetPath = source.AssetPath; break; }
            }
            if (String.IsNullOrEmpty(assetPath) || !String.Equals(absolutePath, assetPath + expectedTail, StringComparison.Ordinal))
                throw new InvalidDataException("下载地址无效；仅允许已启用上游仓库的 HTTPS 发行资产。");
        }

        public static async Task<string> DownloadAsync(ReleaseInfo release, string cacheDirectory, IProgress<DownloadProgress> progress, CancellationToken token)
        {
            if (CurlTransport.IsAvailable)
                return await DownloadCoreAsync(release, cacheDirectory, progress, token, null, true).ConfigureAwait(false);
            using (var client = CreateClient())
                return await DownloadWithClientAsync(release, cacheDirectory, progress, token, client).ConfigureAwait(false);
        }

        internal static Task<string> DownloadWithClientAsync(ReleaseInfo release, string cacheDirectory, IProgress<DownloadProgress> progress, CancellationToken token, HttpClient client)
        {
            return DownloadCoreAsync(release, cacheDirectory, progress, token, client, false);
        }

        static async Task<string> DownloadCoreAsync(ReleaseInfo release, string cacheDirectory, IProgress<DownloadProgress> progress, CancellationToken token, HttpClient client, bool native)
        {
            token.ThrowIfCancellationRequested();
            ValidateRelease(release);
            // Snapshot mutable UI data before the first await.
            string digest = release.Sha256.ToLowerInvariant();
            long size = release.Size;
            string assetPath = release.AssetPath;
            if (String.IsNullOrEmpty(assetPath) && !String.IsNullOrEmpty(release.SourceId))
                foreach (var source in DownloadableSources)
                    if (String.Equals(source.Id, release.SourceId, StringComparison.Ordinal)) { assetPath = source.AssetPath; break; }
            var uri = new Uri(release.DownloadUrl);
            if (String.IsNullOrEmpty(assetPath))
            {
                string absolutePath = Uri.UnescapeDataString(uri.AbsolutePath);
                foreach (var source in DownloadableSources)
                    if (absolutePath.StartsWith(source.AssetPath, StringComparison.Ordinal)) { assetPath = source.AssetPath; break; }
            }
            string part = null;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMinutes(20));
                CancellationToken active = timeout.Token;
                try
                {
                    string directory = Path.GetFullPath(cacheDirectory);
                    Directory.CreateDirectory(directory);
                    string destination = Path.Combine(directory, digest + ".zip");
                    Report(progress, 0, size);
                    active.ThrowIfCancellationRequested();
                    if (await IsValidCacheAsync(destination, size, digest, active).ConfigureAwait(false))
                    {
                        active.ThrowIfCancellationRequested();
                        Report(progress, size, size);
                        return destination;
                    }
                    if (native)
                    {
                        part = Path.Combine(directory, digest + "." + Guid.NewGuid().ToString("N") + ".part");
                        await DownloadNativeAsync(uri, part, size, assetPath, progress, active).ConfigureAwait(false);
                        if (!await IsValidCacheAsync(part, size, digest, active).ConfigureAwait(false))
                            throw new InvalidDataException("下载文件的大小或 SHA-256 与官方记录不符，文件未被采用，请重新下载。");
                    }
                    else using (var response = await SendAsync(client, uri, true, assetPath, active).ConfigureAwait(false))
                    {
                        long? contentLength = response.Content.Headers.ContentLength;
                        if (contentLength.HasValue && contentLength.Value != size) throw new InvalidDataException("服务器返回的文件大小与官方发行记录不符，已停止下载。");
                        part = Path.Combine(directory, digest + "." + Guid.NewGuid().ToString("N") + ".part");
                        using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                        using (var hash = SHA256.Create())
                        {
                            var watch = Stopwatch.StartNew();
                            var buffer = new byte[65536];
                            long received = 0;
                            int read;
                            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, active).ConfigureAwait(false)) != 0)
                            {
                                received += read;
                                if (received > size) throw new InvalidDataException("下载内容超过官方记录的大小，已停止下载。");
                                await output.WriteAsync(buffer, 0, read, active).ConfigureAwait(false);
                                hash.TransformBlock(buffer, 0, read, buffer, 0);
                                if (watch.ElapsedMilliseconds >= 100) { Report(progress, received, size); watch.Restart(); }
                            }
                            if (received != size) throw new InvalidDataException("下载未完成：文件大小与官方记录不符，请重新下载。");
                            hash.TransformFinalBlock(new byte[0], 0, 0);
                            if (!String.Equals(Hex(hash.Hash), digest, StringComparison.Ordinal)) throw new InvalidDataException("发行包 SHA-256 校验失败，文件未被采用，请重新下载。");
                            await output.FlushAsync(active).ConfigureAwait(false);
                        }
                    }
                    active.ThrowIfCancellationRequested();
                    // Publish only a fully checked file; keep the old cache intact until this point.
                    if (File.Exists(destination)) File.Replace(part, destination, null);
                    else File.Move(part, destination);
                    part = null;
                    Report(progress, size, size);
                    return destination;
                }
                catch (OperationCanceledException e)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException("下载已取消。", e, token);
                    throw new TimeoutException("下载 GitHub 发行包超时，请检查网络或系统代理后重试。", e);
                }
                catch (HttpRequestException e) { throw new InvalidOperationException("无法连接 GitHub 下载发行包，请检查网络或系统代理后重试。", e); }
                catch (InvalidDataException) { throw; }
                catch (UnauthorizedAccessException e) { throw new InvalidOperationException("无法写入下载缓存，请选择可写目录后重试。", e); }
                catch (IOException e)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException("下载已取消。", e, token);
                    if (timeout.IsCancellationRequested) throw new TimeoutException("下载 GitHub 发行包超时，请稍后重试。", e);
                    throw new InvalidOperationException("读取或写入发行包失败，请检查网络、缓存目录权限和磁盘空间。", e);
                }
                finally
                {
                    if (part != null) DeleteTemporary(part);
                }
            }
        }

        static async Task DownloadNativeAsync(Uri uri, string part, long size, string assetPath, IProgress<DownloadProgress> progress, CancellationToken token)
        {
            for (int redirects = 0; redirects <= 5; redirects++)
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(part)) File.Delete(part);
                var response = await CurlTransport.FetchToFileAsync(uri, part, size, true, progress, token).ConfigureAwait(false);
                if (IsRedirect(response.StatusCode))
                {
                    if (!AllowedAssetRedirect(response.Redirect, assetPath)) throw new InvalidDataException("GitHub 下载重定向到非官方地址或不安全连接，已停止下载。");
                    uri = response.Redirect;
                    continue;
                }
                CheckStatus(response.StatusCode);
                return;
            }
            throw new InvalidDataException("GitHub 下载重定向次数过多，已停止下载。");
        }

        static void DeleteTemporary(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* An externally locked partial file is never used as a ZIP. */ }
            catch (UnauthorizedAccessException) { }
        }

        static async Task<bool> IsValidCacheAsync(string path, long size, string digest, CancellationToken token)
        {
            if (!File.Exists(path)) return false;
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
            {
                if (source.Length != size) return false;
                using (var hash = SHA256.Create())
                {
                    var buffer = new byte[65536];
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                        hash.TransformBlock(buffer, 0, read, buffer, 0);
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    return String.Equals(Hex(hash.Hash), digest, StringComparison.Ordinal);
                }
            }
        }

        static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        static void Report(IProgress<DownloadProgress> progress, long received, long total)
        {
            if (progress != null) progress.Report(new DownloadProgress { Received = received, Total = total });
        }

        static async Task<HttpResponseMessage> SendAsync(HttpClient client, Uri uri, bool asset, CancellationToken token)
        {
            return await SendAsync(client, uri, asset, null, token).ConfigureAwait(false);
        }

        static async Task<HttpResponseMessage> SendAsync(HttpClient client, Uri uri, bool asset, string assetPath, CancellationToken token)
        {
            for (int redirects = 0; redirects <= 5; redirects++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    request.Headers.Accept.ParseAdd(asset ? "application/octet-stream" : "application/vnd.github+json");
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    int status = (int)response.StatusCode;
                    if (IsRedirect(status))
                    {
                        Uri location = response.Headers.Location;
                        response.Dispose();
                        if (location != null && !location.IsAbsoluteUri) location = new Uri(uri, location);
                        if (!asset || !AllowedAssetRedirect(location, assetPath)) throw new InvalidDataException("GitHub 下载重定向到非官方地址或不安全连接，已停止下载。");
                        uri = location;
                        continue;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        response.Dispose();
                        CheckStatus(status);
                    }
                    if (response.Content == null) { response.Dispose(); throw new InvalidDataException("GitHub 返回了空响应，无法读取发行资源。"); }
                    return response;
                }
            }
            throw new InvalidDataException("GitHub 下载重定向次数过多，已停止下载。");
        }

        static bool IsRedirect(int status) { return status == 301 || status == 302 || status == 303 || status == 307 || status == 308; }
        static void CheckStatus(int status)
        {
            if (status >= 200 && status <= 299) return;
            if (status == 403 || status == 429) throw new InvalidOperationException("GitHub 暂时限制了请求（HTTP " + status + "），请稍后重试。");
            if (status == 404) throw new InvalidOperationException("GitHub 未找到此发行资源（HTTP 404），请刷新版本列表后重试。");
            throw new InvalidOperationException("GitHub 返回 HTTP " + status + "，请稍后重试。");
        }

        static bool AllowedAssetRedirect(Uri uri, string assetPath)
        {
            if (!IsHttps(uri)) return false;
            if (String.Equals(uri.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(uri.Host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)) return true;
            if (!String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
            if (!String.IsNullOrEmpty(assetPath)) return uri.AbsolutePath.StartsWith(assetPath, StringComparison.Ordinal);
            foreach (var source in DownloadableSources)
                if (uri.AbsolutePath.StartsWith(source.AssetPath, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
