using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OptiScalerInstaller
{
    internal sealed class CurlResponse
    {
        internal int StatusCode;
        internal Uri Redirect;
    }

    internal static class CurlTransport
    {
        static string Executable { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe"); } }
        internal static bool IsAvailable { get { return File.Exists(Executable); } }

        // Windows CommandLineToArgvW / CRT quoting, with no command shell involved.
        internal static string QuoteArgument(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0) throw new ArgumentException("进程参数无效。");
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); }
                else { result.Append('\\', slashes); result.Append(c); }
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }

        internal static async Task<CurlResponse> FetchToFileAsync(Uri uri, string destination, long maxBytes, bool asset, IProgress<DownloadProgress> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (uri == null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
                !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Fragment))
                throw new InvalidDataException("仅允许 HTTPS 下载地址。");
            if (maxBytes <= 0 || maxBytes > 1024L * 1024 * 1024) throw new InvalidDataException("下载大小限制无效。");
            if (!IsAvailable) throw new InvalidOperationException("未找到 Windows 自带的 curl.exe。");

            var arguments = new List<string> {
                // -q must be FIRST: never inherit a user's curlrc, credentials or insecure options.
                "-q", "--silent", "--show-error", "--proto", "=https",
                "--connect-timeout", "20", "--max-time", asset ? "1200" : "40",
                "--max-filesize", maxBytes.ToString(CultureInfo.InvariantCulture),
                "--user-agent", "OptiScaler-DLSSNR-Chinese-Installer/1.0",
                "--header", asset ? "Accept: application/octet-stream" : "Accept: application/vnd.github+json",
                "--output", destination, "--write-out", "%{http_code}\\n%{redirect_url}", "--url", uri.AbsoluteUri
            };
            var command = new StringBuilder();
            foreach (string argument in arguments) { if (command.Length > 0) command.Append(' '); command.Append(QuoteArgument(argument)); }

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo {
                    FileName = Executable, Arguments = command.ToString(), UseShellExecute = false,
                    CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                bool started = false;
                try
                {
                    try { started = process.Start(); }
                    catch (Win32Exception e) { throw new InvalidOperationException("无法启动 Windows 自带的 curl，请检查应用运行权限。", e); }
                    if (!started) throw new InvalidOperationException("无法启动 Windows 下载组件。");
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    while (!process.HasExited)
                    {
                        token.ThrowIfCancellationRequested();
                        ReportLength(destination, maxBytes, progress);
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                    string metadata = await stdout.ConfigureAwait(false);
                    // Read stderr to drain the pipe, but never expose signed URLs or proxy details.
                    await stderr.ConfigureAwait(false);
                    int exitCode = process.ExitCode;
                    if (exitCode == 28) throw new TimeoutException("连接或下载 GitHub 资源超时，请稍后重试。");
                    if (exitCode == 63) throw new InvalidDataException("下载内容超过允许的大小，已停止下载。");
                    if (exitCode == 23) throw new InvalidOperationException("无法写入下载缓存，请检查目录权限和磁盘空间。");
                    if (exitCode == 35 || exitCode == 60) throw new InvalidOperationException("无法与 GitHub 建立安全连接，请检查网络、系统时间或证书后重试。");
                    if (exitCode != 0) throw new InvalidOperationException("Windows 下载组件连接失败（curl " + exitCode + "），请检查网络或代理后重试。");
                    ReportLength(destination, maxBytes, progress);
                    string[] fields = metadata.TrimEnd('\r', '\n').Split(new[] { '\n' }, 2);
                    int status;
                    if (fields.Length == 0 || !Int32.TryParse(fields[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out status) || status < 100 || status > 599)
                        throw new InvalidDataException("Windows 下载组件没有返回有效的 HTTP 状态。");
                    Uri redirect = null;
                    if (fields.Length > 1 && !String.IsNullOrWhiteSpace(fields[1]) && !Uri.TryCreate(fields[1].Trim(), UriKind.Absolute, out redirect))
                        throw new InvalidDataException("GitHub 返回了无效的重定向地址。");
                    return new CurlResponse { StatusCode = status, Redirect = redirect };
                }
                finally
                {
                    if (started)
                    {
                        try { if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); } }
                        catch (InvalidOperationException) { }
                        catch (Win32Exception) { }
                    }
                }
            }
        }

        static void ReportLength(string path, long maxBytes, IProgress<DownloadProgress> progress)
        {
            if (!File.Exists(path)) return;
            long length = new FileInfo(path).Length;
            if (length > maxBytes) throw new InvalidDataException("下载内容超过允许的大小，已停止下载。");
            if (progress != null) progress.Report(new DownloadProgress { Received = length, Total = maxBytes });
        }
    }
}
