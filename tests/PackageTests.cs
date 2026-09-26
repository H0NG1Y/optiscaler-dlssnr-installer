using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using OptiScalerInstaller;

public static class PackageTestRunner
{
    const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    const string BaseUrl = "https://github.com/Dagherbou/OptiScaler_DLSSNR/releases/download/";
    const string PresrBaseUrl = "https://github.com/wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass/releases/download/";
    static int failed;
    static string root;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static void Assert(bool yes, string why) { if (!yes) throw new Exception(why); }
    static void Run(string name, Action test) { try { test(); Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    static void Reject(Action test) { bool rejected=false; try { test(); } catch (InvalidDataException) { rejected=true; } catch (InvalidOperationException) { rejected=true; } Assert(rejected, "unsafe package accepted"); }
    static string Dir() { string p=Path.Combine(root,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    static ReleaseInfo Release() { return new ReleaseInfo { Tag="v0.2.0-dlssnr", AssetName="OptiScaler-DLSSNR-v0.2.0.zip", DownloadUrl=BaseUrl+"v0.2.0-dlssnr/OptiScaler-DLSSNR-v0.2.0.zip", Sha256=AbcHash, Size=3, SourceId=PackageService.SourceDagherbou.Id, SourceDisplay=PackageService.SourceDagherbou.DisplayName, AssetPath=PackageService.SourceDagherbou.AssetPath }; }
    static ReleaseInfo PresrRelease() { return new ReleaseInfo { Tag="v0.8.3", AssetName="OptiScaler-NR-v0.8.3.zip", DownloadUrl=PresrBaseUrl+"v0.8.3/OptiScaler-NR-v0.8.3.zip", Sha256=AbcHash, Size=3, SourceId=PackageService.SourcePresrMultipass.Id, SourceDisplay=PackageService.SourcePresrMultipass.DisplayName, AssetPath=PackageService.SourcePresrMultipass.AssetPath }; }
    static object ReleaseJson(string tag, bool draft, bool prerelease, string digest, long size, string url)
    {
        return new { tag_name=tag, draft=draft, prerelease=prerelease, published_at="2026-09-03T19:49:55Z", assets=new[] { new { name="OptiScaler.zip", size=size, digest=digest, browser_download_url=url ?? BaseUrl+tag+"/OptiScaler.zip" } } };
    }
    static object PresrReleaseJson(string tag, string assetName, string digest, long size)
    {
        return new { tag_name=tag, draft=false, prerelease=false, published_at="2026-09-13T06:16:54Z", assets=new[] { new { name=assetName, size=size, digest=digest, browser_download_url=PresrBaseUrl+tag+"/"+assetName } } };
    }
    static object Latest() { return ReleaseJson("v0.2.0-dlssnr",false,false,"sha256:"+AbcHash,3,null); }
    static string Download(ReleaseInfo r,string dir,HttpClient client,IProgress<DownloadProgress> progress,CancellationToken token) { return PackageService.DownloadWithClientAsync(r,dir,progress,token,client).GetAwaiter().GetResult(); }
    sealed class ProgressSink : IProgress<DownloadProgress>
    {
        public DownloadProgress Last;
        public Action<DownloadProgress> OnReport;
        public void Report(DownloadProgress value) { Last=value; if(OnReport!=null) OnReport(value); }
    }
    sealed class Handler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage,HttpResponseMessage> respond;
        public Handler(Func<HttpRequestMessage,HttpResponseMessage> response) { respond=response; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(respond(req)); }
    }
    sealed class SlowStream : MemoryStream
    {
        public SlowStream(byte[] bytes) : base(bytes) {}
        public override async Task<int> ReadAsync(byte[] b,int offset,int count,CancellationToken token) { await Task.Delay(120,token); return await base.ReadAsync(b,offset,Math.Min(count,2),token); }
    }
    sealed class UnknownLengthContent : HttpContent
    {
        readonly Stream stream;
        public UnknownLengthContent(Stream data) { stream=data; }
        protected override bool TryComputeLength(out long length) { length=0; return false; }
        protected override Task SerializeToStreamAsync(Stream target,TransportContext context) { return stream.CopyToAsync(target); }
        protected override Task<Stream> CreateContentReadStreamAsync() { return Task.FromResult(stream); }
        protected override void Dispose(bool disposing) { if(disposing) stream.Dispose(); base.Dispose(disposing); }
    }
    static HttpResponseMessage Body(byte[] bytes) { return new HttpResponseMessage(HttpStatusCode.OK) { Content=new UnknownLengthContent(new MemoryStream(bytes)) }; }
    public static int Main(string[] args)
    {
        root=Path.Combine(Path.GetTempPath(),"OptiScalerPackageTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Run("native arguments preserve spaces quotes and trailing backslashes",delegate {
            Assert(CurlTransport.QuoteArgument("hello world")=="\"hello world\"","space split");
            Assert(CurlTransport.QuoteArgument("a\"b")=="\"a\\\"b\"","quote lost");
            Assert(CurlTransport.QuoteArgument("C:\\folder with space\\")=="\"C:\\folder with space\\\\\"","trailing slash consumes quote");
            Assert(CurlTransport.QuoteArgument("")=="\"\"","empty argument lost");
            Assert(CurlTransport.QuoteArgument("a & b | c")=="\"a & b | c\"","shell characters altered");
        });
        Run("native transport rejects HTTP before process launch",delegate { string file=Path.Combine(Dir(),"blocked.part"); Reject(delegate { CurlTransport.FetchToFileAsync(new Uri("http://github.com/example"),file,3,true,null,CancellationToken.None).GetAwaiter().GetResult(); }); Assert(!File.Exists(file),"insecure request wrote file"); });
        Run("native cancel before launch writes no part",delegate { string file=Path.Combine(Dir(),"canceled.part"); var cancel=new CancellationTokenSource(); cancel.Cancel(); bool canceled=false; try { CurlTransport.FetchToFileAsync(new Uri(Release().DownloadUrl),file,3,true,null,cancel.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { canceled=true; } Assert(canceled && !File.Exists(file),"native cancel ignored"); });
        Run("official latest precedes newer patch release",delegate {
            var list=PackageService.ParseReleases(Json.Serialize(Latest()),Json.Serialize(new[] { ReleaseJson("v0.2.0-patch1",false,false,"sha256:"+AbcHash,3,null), Latest() }));
            Assert(list.Count==2,"release count"); Assert(list[0].Tag=="v0.2.0-dlssnr" && list[0].IsLatest,"newer patch became default"); Assert(!list[1].IsLatest,"duplicate latest designation");
        });
        Run("draft prerelease unsafe and unverifiable assets excluded",delegate {
            var data=new[] { Latest(), ReleaseJson("draft",true,false,"sha256:"+AbcHash,3,null), ReleaseJson("beta",false,true,"sha256:"+AbcHash,3,null), ReleaseJson("bad-hash",false,false,null,3,null), ReleaseJson("foreign",false,false,"sha256:"+AbcHash,3,"https://evil.test/a.zip"), ReleaseJson("large",false,false,"sha256:"+AbcHash,1073741825L,null) };
            Assert(PackageService.ParseReleases(Json.Serialize(Latest()),Json.Serialize(data)).Count==1,"unsafe asset listed");
        });
        Run("latest included when not on first releases page",delegate { var list=PackageService.ParseReleases(Json.Serialize(Latest()),"[]"); Assert(list.Count==1 && list[0].IsLatest,"latest omitted"); });
        Run("unverifiable latest cannot silently select another default",delegate { Reject(delegate { PackageService.ParseReleases(Json.Serialize(ReleaseJson("latest",false,false,null,3,null)),Json.Serialize(new[] { Latest() })); }); });
        Run("malformed API response rejected",delegate { Reject(delegate { PackageService.ParseReleases("{broken", "[]"); }); });
        Run("presr multipass source lists tagged assets",delegate {
            var latest=PresrReleaseJson("v0.8.3","OptiScaler-NR-v0.8.3.zip","sha256:"+AbcHash,3);
            var list=PackageService.ParseReleases(PackageService.SourcePresrMultipass,Json.Serialize(latest),Json.Serialize(new[] { latest, PresrReleaseJson("v0.8.3","OptiScaler-NR-v0.8.3-rtx40-mfg.zip","sha256:"+AbcHash,3) }));
            Assert(list.Count==2,"presr asset count");
            Assert(list[0].SourceId=="presr-multipass" && list[0].AssetPath==PackageService.SourcePresrMultipass.AssetPath,"presr metadata missing");
            Assert(list[0].ToString().Contains("PreSR-Multipass"),"presr display missing in ToString");
        });
        Run("presr assets are not listed under dagherbou source",delegate {
            var foreign=PresrReleaseJson("v0.8.3","OptiScaler-NR-v0.8.3.zip","sha256:"+AbcHash,3);
            Reject(delegate { PackageService.ParseReleases(PackageService.SourceDagherbou,Json.Serialize(foreign),Json.Serialize(new[] { foreign })); });
        });
        Run("download accepts presr official asset and rejects foreign owner",delegate {
            string d=Dir(); using(var c=new HttpClient(new Handler(delegate { return Body(Encoding.ASCII.GetBytes("abc")); }))) {
                string file=Download(PresrRelease(),d,c,null,CancellationToken.None);
                Assert(Path.GetFileName(file)==AbcHash+".zip","presr cache name");
            }
            foreach(string url in new[] {
                PresrBaseUrl+"v0.8.3/OptiScaler-NR-v0.8.3.zip",
                "https://github.com/Other/OptiScaler-DLSSNR-PreSR-Multipass/releases/download/v0.8.3/OptiScaler-NR-v0.8.3.zip",
                "https://github.com/wilsjo2/OptiScaler_DLSSNR/releases/download/v0.8.3/OptiScaler-NR-v0.8.3.zip" }) {
                var r=PresrRelease(); r.DownloadUrl=url; r.AssetPath=null; r.SourceId=null;
                if (url.Contains("/Other/")||url.Contains("OptiScaler_DLSSNR/releases")) {
                    string d2=Dir(); using(var c=new HttpClient(new Handler(delegate { throw new Exception("network reached"); }))) Reject(delegate { Download(r,d2,c,null,CancellationToken.None); });
                }
            }
        });
        Run("untrusted URL rejected before cache access",delegate {
            foreach(string url in new[] { "http://github.com/Dagherbou/OptiScaler_DLSSNR/releases/download/v0.2.0-dlssnr/OptiScaler-DLSSNR-v0.2.0.zip", "https://github.com.evil.test/a.zip", "https://github.com/Other/OptiScaler_DLSSNR/releases/download/v0.2.0-dlssnr/OptiScaler-DLSSNR-v0.2.0.zip", "https://attacker@github.com/Dagherbou/OptiScaler_DLSSNR/releases/download/v0.2.0-dlssnr/OptiScaler-DLSSNR-v0.2.0.zip" }) {
                var r=Release(); r.DownloadUrl=url; r.AssetPath=null; r.SourceId=null; string d=Dir(); using(var c=new HttpClient(new Handler(delegate { throw new Exception("network reached"); }))) Reject(delegate { Download(r,d,c,null,CancellationToken.None); }); Assert(Directory.GetFiles(d).Length==0,"unsafe cache write");
            }
        });
        Run("verified stream becomes hash-named cache and reports completion",delegate {
            string d=Dir(); var p=new ProgressSink(); using(var c=new HttpClient(new Handler(delegate { return Body(Encoding.ASCII.GetBytes("abc")); }))) { string file=Download(Release(),d,c,p,CancellationToken.None); Assert(Path.GetFileName(file)==AbcHash+".zip","cache not content-addressed"); Assert(File.ReadAllText(file)=="abc","download changed"); Assert(p.Last.Received==3 && p.Last.Total==3,"missing completion progress"); Assert(Directory.GetFiles(d,"*.part").Length==0,"temporary retained"); }
        });
        Run("same-size corrupt cache is rehashed and repaired",delegate {
            string d=Dir(); File.WriteAllText(Path.Combine(d,AbcHash+".zip"),"bad"); using(var c=new HttpClient(new Handler(delegate { return Body(Encoding.ASCII.GetBytes("abc")); }))) Assert(File.ReadAllText(Download(Release(),d,c,null,CancellationToken.None))=="abc","corrupt cached file reused");
        });
        Run("valid cache works without network",delegate {
            string d=Dir(); File.WriteAllText(Path.Combine(d,AbcHash+".zip"),"abc"); using(var c=new HttpClient(new Handler(delegate { throw new Exception("unexpected network"); }))) Assert(File.ReadAllText(Download(Release(),d,c,null,CancellationToken.None))=="abc","valid cache rejected");
        });
        Run("hash mismatch never publishes final file",delegate {
            string d=Dir(); using(var c=new HttpClient(new Handler(delegate { return Body(Encoding.ASCII.GetBytes("bad")); }))) Reject(delegate { Download(Release(),d,c,null,CancellationToken.None); }); Assert(Directory.GetFiles(d).Length==0,"bad or partial package retained");
        });
        Run("short and oversized streams rejected",delegate {
            foreach(string value in new[] { "ab", "abcd" }) { string d=Dir(); using(var c=new HttpClient(new Handler(delegate { return Body(Encoding.ASCII.GetBytes(value)); }))) Reject(delegate { Download(Release(),d,c,null,CancellationToken.None); }); Assert(Directory.GetFiles(d).Length==0,"invalid length cached"); }
        });
        Run("cancel before transfer creates no cache files",delegate {
            string d=Dir(); var source=new CancellationTokenSource(); source.Cancel(); bool canceled=false; using(var c=new HttpClient(new Handler(delegate { throw new Exception("network reached"); }))) try { Download(Release(),d,c,null,source.Token); } catch(OperationCanceledException) { canceled=true; } Assert(canceled,"cancel ignored"); Assert(Directory.GetFiles(d).Length==0,"cancel writes");
        });
        Run("cancel while streaming removes part file",delegate {
            string d=Dir(); var source=new CancellationTokenSource(); var p=new ProgressSink { OnReport=delegate(DownloadProgress x) { if(x.Received>0) source.Cancel(); } }; bool canceled=false;
            using(var c=new HttpClient(new Handler(delegate { return new HttpResponseMessage(HttpStatusCode.OK) { Content=new UnknownLengthContent(new SlowStream(Encoding.ASCII.GetBytes("abc"))) }; }))) try { Download(Release(),d,c,p,source.Token); } catch(OperationCanceledException) { canceled=true; } Assert(canceled,"stream cancel ignored"); Assert(Directory.GetFiles(d).Length==0,"cancel leaves partial");
        });
        Run("redirect cannot downgrade TLS or leave GitHub",delegate {
            foreach(string url in new[] { "http://release-assets.githubusercontent.com/a", "https://evil.test/a.zip" }) { string d=Dir(); using(var c=new HttpClient(new Handler(delegate { var response=new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location=new Uri(url); return response; }))) Reject(delegate { Download(Release(),d,c,null,CancellationToken.None); }); Assert(Directory.GetFiles(d).Length==0,"unsafe redirect written"); }
        });
        Run("GitHub signed asset redirect preserves integrity verification",delegate {
            string d=Dir(); using(var c=new HttpClient(new Handler(delegate(HttpRequestMessage request) { if(request.RequestUri.Host=="github.com") { var response=new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location=new Uri("https://release-assets.githubusercontent.com/github-production-release-asset/a?token=test"); return response; } return Body(Encoding.ASCII.GetBytes("abc")); }))) Assert(File.ReadAllText(Download(Release(),d,c,null,CancellationToken.None))=="abc","official asset redirect failed");
        });
        Run("rate limit gives actionable Chinese error",delegate {
            string message=""; using(var c=new HttpClient(new Handler(delegate { return new HttpResponseMessage((HttpStatusCode)429); }))) try { Download(Release(),Dir(),c,null,CancellationToken.None); } catch(InvalidOperationException e) { message=e.Message; } Assert(message.Contains("429") && message.Contains("稍后"),"rate limit not explained");
        });
        if(args.Length>0 && args[0]=="--live-cancel") Run("live native cancellation closes process and removes partial file",delegate {
            Assert(args.Length>2,"supply workspace cache directory and captured release JSON");
            var release=PackageService.ParseReleases(File.ReadAllText(args[2]),"[]")[0];
            string cache=Path.Combine(args[1],"取消 测试-"+Guid.NewGuid().ToString("N")); long observed=0; bool canceled=false;
            using(var cancel=new CancellationTokenSource()) {
                cancel.CancelAfter(TimeSpan.FromSeconds(30));
                var progress=new ProgressSink { OnReport=delegate(DownloadProgress value) { if(value.Received>0) { observed=value.Received; cancel.Cancel(); } } };
                try { PackageService.DownloadAsync(release,cache,progress,cancel.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { canceled=true; }
            }
            Assert(canceled && observed>0,"native transfer did not cancel after receiving bytes");
            Assert(Directory.GetFiles(cache).Length==0,"native cancellation left an open or partial file");
            Console.WriteLine("  Canceled after "+observed+" bytes; cache contains no partial files.");
        });
        else if(args.Length>0 && (args[0]=="--live" || args[0]=="--live-download"))
        {
            ReleaseInfo liveLatest=null;
            Run("live official release API",delegate { var list=PackageService.FetchReleasesAsync(CancellationToken.None).GetAwaiter().GetResult(); Assert(list.Count>0 && list[0].IsLatest,"live latest missing"); liveLatest=list[0]; foreach(var release in list) Console.WriteLine("  "+release.ToString()); });
            if(args[0]=="--live-download") Run("live official asset download through public API",delegate {
                Assert(args.Length>1,"supply a workspace cache directory");
                if(liveLatest==null && args.Length>2) liveLatest=PackageService.ParseReleases(File.ReadAllText(args[2]),"[]")[0];
                Assert(liveLatest!=null,"no verified release metadata available");
                string cache=Path.Combine(args[1],"live-"+Guid.NewGuid().ToString("N")); long printed=0;
                var progress=new ProgressSink { OnReport=delegate(DownloadProgress value) { if(value.Received-printed>=10*1024*1024 || value.Received==value.Total) { printed=value.Received; Console.WriteLine("  Downloaded "+value.Received+" / "+value.Total); } } };
                string downloaded=PackageService.DownloadAsync(liveLatest,cache,progress,CancellationToken.None).GetAwaiter().GetResult();
                Assert(new FileInfo(downloaded).Length==liveLatest.Size,"live package length mismatch");
                using(var hash=SHA256.Create()) using(var stream=File.OpenRead(downloaded)) { string actual=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); Assert(actual==liveLatest.Sha256,"live package digest mismatch"); Console.WriteLine("  Verified SHA256: "+actual); }
                Console.WriteLine("  Verified package: "+downloaded);
            });
        }
        else if(args.Length>0) Run("captured official latest response parses",delegate { string real=File.ReadAllText(args[0]); var list=PackageService.ParseReleases(real,"[]"); Assert(list[0].Tag=="v0.2.0-dlssnr" && list[0].Size==130486024L,"official schema rejected"); Assert(list[0].Sha256=="8eece7a4d7de6de5917f0c99ac60540b2d77022e7699bba717b0a6d9e1829bce","official digest changed"); });
        Console.WriteLine("Failures: "+failed+"; fixtures: "+root); return failed==0 ? 0 : 1;
    }
}
