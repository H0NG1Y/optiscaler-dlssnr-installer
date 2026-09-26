using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using OptiScalerInstaller;

public static class TestRunner
{
    static int failed;
    static string root;
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } catch (IOException) { rejected = true; } Assert(rejected, "Expected operation rejection"); }
    static void Run(string name, Action test) { try { test(); Console.WriteLine("PASS " + name); } catch(Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    static string Dir() { string p=Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    public static byte[] Pe(bool dll) { byte[] b = new byte[1024]; b[0]=77; b[1]=90; b[60]=128; b[128]=80; b[129]=69; b[132]=0x64; b[133]=0x86; b[150]=2; b[151]=(byte)(dll ? 0x20 : 0); b[152]=0x0b; b[153]=2; return b; }
    static string Game(string dir) { string p=Path.Combine(dir,"Game.exe"); File.WriteAllBytes(p,Pe(false)); return p; }
    static string Zip(string dir, string version, string extra) { string p=Path.Combine(dir,"package-"+Guid.NewGuid().ToString("N")+".zip"); using(var z=ZipFile.Open(p,ZipArchiveMode.Create)) { Put(z,"OptiScaler.dll",Pe(true)); Put(z,"nvngx.dll_dlssnr.dll",Pe(true)); Put(z,"OptiScaler.ini",Encoding.UTF8.GetBytes("[Other]\r\nEnabled=false\r\n[DlssNr]\r\nEnabled=false\r\n[Spoofing]\r\nDxgi=auto\r\n")); Put(z,"OptiScaler\\component.dll",Encoding.UTF8.GetBytes(version)); Put(z,"Licenses\\notice.txt",Encoding.UTF8.GetBytes("license")); Put(z,"setup_windows.bat",Encoding.UTF8.GetBytes("do not run")); if(extra!=null) Put(z,extra,Encoding.UTF8.GetBytes("escape")); } return p; }
    static void Put(ZipArchive z,string path,byte[] b) { using(var s=z.CreateEntry(path).Open()) s.Write(b,0,b.Length); }
    static InstallOptions Options(string exe) { return new InstallOptions { GameExe=exe, EnableNeuralRendering=false }; }
    static void Install(string zip,InstallOptions o,string v) { InstallerEngine.Install(zip,o,v,delegate{},CancellationToken.None); }
    const string Enabler = "dlss-enabler-headless.dll", Nukem = "dlssg_to_fsr3_amd_is_better.dll";
    static readonly string[] DlssgRequired = { "sl.interposer.dll", "sl.common.dll", "nvngx_dlssg.dll", "sl.dlss_g.dll", "sl.reflex.dll", "sl.pcl.dll" };
    static readonly string[] DlssgExtraDlls = { "nvngx_dlss.dll", "sl.dlss.dll", "sl.dlss_nr.dll", "sl.nis.dll" };
    static readonly string[] DlssgExtraLicenses = { "nis.license.txt" };
    static readonly string[] DlssgLicenses = { "reflex.license.txt", "nvngx_dlss.license.txt" };
    static string DlssgRelative(string name) { return "OptiScaler\\streamline\\"+name; }
    static string DlssgTarget(string gameDirectory,string name) { return Path.Combine(gameDirectory,"OptiScaler","streamline",name); }
    static string DlssgSources(int licenses,byte marker)
    {
        string d=Dir(); for(int i=0;i<DlssgRequired.Length;i++) File.WriteAllBytes(Path.Combine(d,DlssgRequired[i]),MarkedPe((byte)(marker+i)));
        for(int i=0;i<DlssgLicenses.Length;i++) if((licenses&(1<<i))!=0) File.WriteAllText(Path.Combine(d,DlssgLicenses[i]),"license "+marker+" "+DlssgLicenses[i]);
        return d;
    }
    static string FsrTarget(string gameDirectory,string name) { return Path.Combine(gameDirectory,"OptiScaler",name); }
    static byte[] MarkedPe(byte marker) { byte[] bytes=Pe(true); bytes[300]=marker; return bytes; }
    static string FsrSources(bool enabler,bool nukem,byte marker) { string d=Dir(); if(enabler) File.WriteAllBytes(Path.Combine(d,Enabler),MarkedPe(marker)); if(nukem) File.WriteAllBytes(Path.Combine(d,Nukem),MarkedPe((byte)(marker+1))); return d; }
    static void AssertBytes(string path,byte[] expected,string message) { Assert(File.Exists(path)&&Convert.ToBase64String(File.ReadAllBytes(path))==Convert.ToBase64String(expected),message); }
    static void AssertNoInstall(string gameDirectory) { Assert(!File.Exists(Path.Combine(gameDirectory,"dxgi.dll")),"proxy partially committed"); Assert(!Directory.Exists(Path.Combine(gameDirectory,"OptiScaler")),"runtime or FSR partially committed"); Assert(!Directory.Exists(Path.Combine(gameDirectory,InstallerEngine.StateDirectoryName)),"state partially committed"); }
    public static int Main(string[] args)
    {
        if(args.Length==3&&args[0]=="--crash-update")
        {
            InstallerEngine.Install(args[2],Options(args[1]),"interrupted",delegate(string message) { if(message=="已处理：OptiScaler\\component.dll") Environment.Exit(73); },CancellationToken.None);
            return 74;
        }
        root=Path.Combine(Path.GetTempPath(),"OptiScalerInstallerTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Run("install and restore original content",delegate {
            string d=Dir(), exe=Game(d), zip=Zip(d,"one",null); File.WriteAllText(Path.Combine(d,"nvngx.dll_dlssnr.dll"),"original bridge");
            Install(zip,Options(exe),"one"); Assert(File.Exists(Path.Combine(d,"dxgi.dll")),"proxy missing"); Assert(!File.Exists(Path.Combine(d,"setup_windows.bat")),"batch copied"); Assert(File.ReadAllText(Path.Combine(d,"Licenses","notice.txt"))=="license","license missing");
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); Assert(!File.Exists(Path.Combine(d,"dxgi.dll")),"proxy retained"); Assert(File.ReadAllText(Path.Combine(d,"nvngx.dll_dlssnr.dll"))=="original bridge","original not restored"); Assert(File.Exists(exe),"game removed");
        });
        Run("update retains first backup and user INI settings",delegate {
            string d=Dir(),exe=Game(d); File.WriteAllText(Path.Combine(d,"OptiScaler.ini"),"[Other]\nCustom=mine\n[DlssNr]\nEnabled=false\n");
            Install(Zip(d,"one",null),Options(exe),"one"); File.AppendAllText(Path.Combine(d,"OptiScaler.ini"),"\n[User]\nValue=42\n"); Install(Zip(d,"two",null),Options(exe),"two");
            Assert(File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="two","update missing"); Assert(File.ReadAllText(Path.Combine(d,"OptiScaler.ini")).Contains("Value=42"),"custom setting lost");
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); Assert(File.ReadAllText(Path.Combine(d,"OptiScaler.ini"))=="[Other]\nCustom=mine\n[DlssNr]\nEnabled=false\n","baseline changed");
        });
        Run("foreign proxy is preserved",delegate { string d=Dir(),exe=Game(d); File.WriteAllText(Path.Combine(d,"dxgi.dll"),"reshade"); Reject(delegate{Install(Zip(d,"one",null),Options(exe),"one");}); Assert(File.ReadAllText(Path.Combine(d,"dxgi.dll"))=="reshade","foreign proxy overwritten"); });
        Run("zip traversal rejected before writes",delegate { string d=Dir(),exe=Game(d); Reject(delegate{Install(Zip(d,"one","../outside.txt"),Options(exe),"one");}); Assert(!File.Exists(Path.Combine(d,"dxgi.dll")),"partial install"); Assert(!File.Exists(Path.Combine(root,"outside.txt")),"escaped zip"); });
        Run("community package extra assets are skipped not rejected",delegate {
            string d=Dir(),exe=Game(d),zip=Path.Combine(d,"presr-like.zip");
            string bridge=Path.Combine(Dir(),"nvngx.dll_dlssnr.dll"); File.WriteAllBytes(bridge,Pe(true));
            using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)) {
                Put(z,"OptiScaler.dll",Pe(true)); Put(z,"nvngx.dll_dlssnr.dll",Pe(true)); Put(z,"OptiScaler.ini",Encoding.UTF8.GetBytes("[DlssNr]\r\nEnabled=false\r\n")); Put(z,"OptiScaler\\component.dll",Encoding.UTF8.GetBytes("presr"));
                Put(z,"images\\bmac.png",Encoding.UTF8.GetBytes("png-bytes")); Put(z,"images\\gh-sponsor-red.png",Encoding.UTF8.GetBytes("png-bytes"));
                Put(z,"docs\\NR-RESTRICTION-AUDIT.md",Encoding.UTF8.GetBytes("audit")); Put(z,"setup_windows.bat",Encoding.UTF8.GetBytes("do not run"));
                Put(z,"redist\\streamline\\sl.interposer.dll",Pe(true)); Put(z,"get_streamline.ps1",Encoding.UTF8.GetBytes("skip"));
            }
            Install(zip,Options(exe),"v0.8.3");
            Assert(File.Exists(Path.Combine(d,"OptiScaler.dll"))||File.Exists(Path.Combine(d,"winmm.dll"))||File.Exists(Path.Combine(d,"dxgi.dll")),"proxy/runtime missing");
            Assert(File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="presr","runtime dll missing");
            Assert(!File.Exists(Path.Combine(d,"images","bmac.png"))&&!Directory.Exists(Path.Combine(d,"images")),"image extracted to game");
            Assert(!File.Exists(Path.Combine(d,"setup_windows.bat"))&& !File.Exists(Path.Combine(d,"get_streamline.ps1")),"script extracted");
            Assert(File.Exists(Path.Combine(d,"docs","NR-RESTRICTION-AUDIT.md")),"allowed md skipped");
            Assert(File.Exists(Path.Combine(d,"redist","streamline","sl.interposer.dll")),"nested dll skipped");
            Assert(InstallerEngine.ReadManifest(exe).Version=="v0.8.3","install failed after skip");
        });
        Run("preSR-like zip missing bridge uses installer fallback",delegate {
            string d=Dir(),exe=Game(d),zip=Path.Combine(d,"presr-no-bridge.zip");
            string bridge=Path.Combine(Dir(),"nvngx.dll_dlssnr.dll"); File.WriteAllBytes(bridge,MarkedPe(200));
            using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)) {
                Put(z,"OptiScaler.dll",Pe(true)); Put(z,"OptiScaler.ini",Encoding.UTF8.GetBytes("[DlssNr]\r\nEnabled=false\r\n")); Put(z,"OptiScaler\\component.dll",Encoding.UTF8.GetBytes("presr"));
                Put(z,"images\\bmac.png",Encoding.UTF8.GetBytes("png"));
            }
            var options=Options(exe); options.BridgeDllFallback=bridge;
            Install(zip,options,"v0.8.3");
            AssertBytes(Path.Combine(d,"nvngx.dll_dlssnr.dll"),MarkedPe(200),"bridge fallback not installed");
            Assert(InstallerEngine.ReadManifest(exe).Files.Exists(f=>f.Path=="nvngx.dll_dlssnr.dll"),"bridge not in manifest");
        });
        Run("preSR-like zip missing bridge without fallback rejected",delegate {
            string d=Dir(),exe=Game(d),zip=Path.Combine(d,"presr-no-bridge-none.zip");
            using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)) {
                Put(z,"OptiScaler.dll",Pe(true)); Put(z,"OptiScaler.ini",Encoding.UTF8.GetBytes("[DlssNr]\r\nEnabled=false\r\n")); Put(z,"OptiScaler\\component.dll",Encoding.UTF8.GetBytes("presr"));
            }
            var options=Options(exe); options.BridgeDllFallback=Path.Combine(Dir(),"missing-nvngx.dll_dlssnr.dll");
            Reject(delegate{Install(zip,options,"v0.8.3");}); AssertNoInstall(d);
        });
        Run("zip package missing required files still rejected",delegate {
            string d=Dir(),exe=Game(d),zip=Path.Combine(d,"incomplete.zip");
            using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)) { Put(z,"images\\bmac.png",Encoding.UTF8.GetBytes("png")); Put(z,"OptiScaler\\component.dll",Pe(true)); }
            Reject(delegate{Install(zip,Options(exe),"one");}); AssertNoInstall(d);
        });
        Run("zip duplicate destination rejected",delegate { string d=Dir(),exe=Game(d); Reject(delegate{Install(Zip(d,"one","OPTISCALER/COMPONENT.dll"),Options(exe),"one");}); Assert(!File.Exists(Path.Combine(d,"dxgi.dll")),"partial install"); });
        Run("modified managed DLL blocks uninstall atomically",delegate { string d=Dir(),exe=Game(d); Install(Zip(d,"one",null),Options(exe),"one"); File.WriteAllText(Path.Combine(d,"OptiScaler","component.dll"),"user replacement"); Reject(delegate{InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);}); Assert(File.Exists(Path.Combine(d,"dxgi.dll")),"partial uninstall"); Assert(File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="user replacement","user file removed"); });
        Run("corrupt original backup blocks uninstall",delegate { string d=Dir(),exe=Game(d); File.WriteAllText(Path.Combine(d,"nvngx.dll_dlssnr.dll"),"original"); Install(Zip(d,"one",null),Options(exe),"one"); var m=InstallerEngine.ReadManifest(exe); var f=m.Files.Find(x=>x.HadOriginal); File.WriteAllText(Path.Combine(d,InstallerEngine.StateDirectoryName,"originals",f.BackupName),"corrupt"); Reject(delegate{InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);}); Assert(File.Exists(Path.Combine(d,"dxgi.dll")),"partial uninstall"); });
        Run("missing model blocks enabled NR",delegate { string d=Dir(),exe=Game(d); var o=Options(exe); o.EnableNeuralRendering=true; Reject(delegate{Install(Zip(d,"one",null),o,"one");}); Assert(!File.Exists(Path.Combine(d,"dxgi.dll")),"installed without model"); });
        Run("model copy and section-specific settings",delegate { string d=Dir(),exe=Game(d),model=Path.Combine(Dir(),"nvngx_dlssnr.dll"); File.WriteAllBytes(model,Pe(true)); var o=Options(exe); o.ModelDll=model; o.EnableNeuralRendering=true; o.Gpu="AMD"; o.UseDlssInputs=false; Install(Zip(d,"one",null),o,"one"); var ini=File.ReadAllText(Path.Combine(d,"OptiScaler.ini")); Assert(ini.Contains("[Other]\r\nEnabled=false"),"unrelated Enabled modified"); Assert(ini.Contains("[DlssNr]\r\nEnabled=true"),"NR disabled"); Assert(ini.Contains("Dxgi=false"),"spoofing not configured"); InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); Assert(!File.Exists(Path.Combine(d,"nvngx_dlssnr.dll")),"copied model not removed"); Assert(File.Exists(model),"source model removed"); });
        Run("preexisting model not adopted",delegate { string d=Dir(),exe=Game(d); File.WriteAllBytes(Path.Combine(d,"nvngx_dlssnr.dll"),Pe(true)); var o=Options(exe); o.EnableNeuralRendering=true; Install(Zip(d,"one",null),o,"one"); InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); Assert(File.Exists(Path.Combine(d,"nvngx_dlssnr.dll")),"existing model removed"); });
        Run("cancel before installation leaves game unchanged",delegate { string d=Dir(),exe=Game(d); var c=new CancellationTokenSource(); c.Cancel(); try { InstallerEngine.Install(Zip(d,"one",null),Options(exe),"one",delegate{},c.Token); throw new Exception("ignored cancel"); } catch(OperationCanceledException) {} Assert(!File.Exists(Path.Combine(d,"dxgi.dll")),"partial install"); });
        Run("write failure rolls back earlier writes",delegate { string d=Dir(),exe=Game(d); Install(Zip(d,"one",null),Options(exe),"one"); string locked=Path.Combine(d,"OptiScaler","component.dll"); using(var s=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.Read)) { Reject(delegate{Install(Zip(d,"two",null),Options(exe),"two");}); } Assert(File.ReadAllText(locked)=="one","old DLL changed"); Assert(InstallerEngine.ReadManifest(exe).Version=="one","manifest changed"); });
        Run("32-bit game rejected",delegate { string d=Dir(),exe=Game(d); byte[] b=Pe(false); b[132]=0x4c; b[133]=1; File.WriteAllBytes(exe,b); Reject(delegate{Install(Zip(d,"one",null),Options(exe),"one");}); });
        Run("proxy switch requires removal first",delegate { string d=Dir(),exe=Game(d); Install(Zip(d,"one",null),Options(exe),"one"); var o=Options(exe); o.ProxyName="winmm.dll"; Reject(delegate{Install(Zip(d,"two",null),o,"two");}); Assert(!File.Exists(Path.Combine(d,"winmm.dll")),"double proxy"); });
        Run("external change during extraction is preserved",delegate {
            string d=Dir(),exe=Game(d); Install(Zip(d,"one",null),Options(exe),"one"); string target=Path.Combine(d,"OptiScaler","component.dll");
            Reject(delegate { InstallerEngine.Install(Zip(d,"two",null),Options(exe),"two",delegate(string message) { if(message.StartsWith("检查")) File.WriteAllText(target,"external update"); },CancellationToken.None); });
            Assert(File.ReadAllText(target)=="external update","concurrent edit overwritten"); Assert(InstallerEngine.ReadManifest(exe).Version=="one","manifest advanced despite conflict");
        });
        Run("process interruption recovers entire previous install",delegate {
            string d=Dir(),exe=Game(d); Install(Zip(d,"one",null),Options(exe),"one"); string next=Zip(d,"two",null);
            var start=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--crash-update \""+exe+"\" \""+next+"\"") { UseShellExecute=false,CreateNoWindow=true };
            using(var child=Process.Start(start)) { Assert(child.WaitForExit(20000),"child timed out"); Assert(child.ExitCode==73,"child did not interrupt commit"); }
            Assert(File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="two","interrupted write was not exercised");
            Reject(delegate{Install(next,Options(exe),"two");}); InstallerEngine.Recover(exe,delegate{});
            Assert(File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="one","recovery did not restore DLL"); Assert(InstallerEngine.ReadManifest(exe).Version=="one","recovery did not restore version");
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); Assert(!File.Exists(Path.Combine(d,"dxgi.dll")),"uninstall failed after recovery");
        });
        for(int selection=1;selection<=3;selection++)
        {
            int selected=selection;
            Run("optional FSR deploy and remove selection "+selected,delegate {
                string d=Dir(),exe=Game(d); bool enabler=(selected&1)!=0,nukem=(selected&2)!=0;
                string sources=FsrSources(enabler,nukem,10); var options=Options(exe); options.FsrDirectory=sources; options.InstallFsrEnabler=enabler; options.InstallFsrNukem=nukem;
                File.WriteAllText(Path.Combine(d,"OptiScaler.ini"),"[FrameGen]\nEnabled=false\nFGType=keep-choice\n");
                Install(Zip(d,"one",null),options,"one"); var manifest=InstallerEngine.ReadManifest(exe);
                foreach(string name in new[]{Enabler,Nukem}) {
                    bool requested=name==Enabler?enabler:nukem; var record=manifest.Files.Find(f=>f.Path=="OptiScaler\\"+name);
                    Assert(File.Exists(FsrTarget(d,name))==requested,"unexpected FSR file selection: "+name);
                    Assert((record!=null)==requested,"unexpected FSR manifest selection: "+name);
                    if(requested) { AssertBytes(FsrTarget(d,name),File.ReadAllBytes(Path.Combine(sources,name)),"FSR bytes differ"); Assert(!record.HadOriginal,"new FSR incorrectly marked original"); }
                }
                Assert(File.ReadAllText(Path.Combine(d,"OptiScaler.ini")).Contains("[FrameGen]\r\nEnabled=false\r\nFGType=keep-choice"),"FrameGen settings changed");
                InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);
                Assert(!File.Exists(FsrTarget(d,Enabler))&&!File.Exists(FsrTarget(d,Nukem)),"new FSR files retained after uninstall");
                Assert(Directory.GetFiles(sources).Length==(selected==3?2:1),"source FSR files changed");
            });
        }
        Run("unselected managed FSR survives a normal update",delegate {
            string d=Dir(),exe=Game(d),sources=FsrSources(true,true,20); var options=Options(exe); options.FsrDirectory=sources; options.InstallFsrEnabler=true; options.InstallFsrNukem=true;
            Install(Zip(d,"one",null),options,"one"); var before=InstallerEngine.ReadManifest(exe);
            var uncheckedOptions=Options(exe); uncheckedOptions.FsrDirectory=Path.Combine(d,"unused missing folder");
            Install(Zip(d,"two",null),uncheckedOptions,"two"); var after=InstallerEngine.ReadManifest(exe);
            Assert(after.Version=="two"&&File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="two","normal update did not complete");
            foreach(string name in new[]{Enabler,Nukem}) { var first=before.Files.Find(f=>f.Path=="OptiScaler\\"+name); var kept=after.Files.Find(f=>f.Path==first.Path); Assert(kept!=null&&kept.InstalledHash==first.InstalledHash&&kept.HadOriginal==first.HadOriginal,"unchecked FSR record discarded"); AssertBytes(FsrTarget(d,name),File.ReadAllBytes(Path.Combine(sources,name)),"unchecked FSR bytes changed"); }
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); Assert(!File.Exists(FsrTarget(d,Enabler))&&!File.Exists(FsrTarget(d,Nukem)),"preserved FSR not removed on uninstall");
        });
        Run("unselected unmanaged FSR is never adopted from game or ZIP",delegate {
            string d=Dir(),exe=Game(d); Directory.CreateDirectory(Path.Combine(d,"OptiScaler"));
            foreach(string name in new[]{Enabler,Nukem}) File.WriteAllText(FsrTarget(d,name),"existing user "+name);
            string zip=Zip(d,"one",null); using(var archive=ZipFile.Open(zip,ZipArchiveMode.Update)) foreach(string name in new[]{Enabler,Nukem}) Put(archive,"OptiScaler/"+name,MarkedPe(40));
            Install(zip,Options(exe),"one"); Assert(!InstallerEngine.ReadManifest(exe).Files.Exists(f=>f.Path=="OptiScaler\\"+Enabler||f.Path=="OptiScaler\\"+Nukem),"unchecked FSR adopted");
            Install(Zip(d,"two",null),Options(exe),"two"); InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);
            foreach(string name in new[]{Enabler,Nukem}) Assert(File.ReadAllText(FsrTarget(d,name))=="existing user "+name,"unmanaged FSR changed or removed");
        });
        Run("FSR checked update preserves first originals for uninstall",delegate {
            string d=Dir(),exe=Game(d),sources=FsrSources(true,true,50); Directory.CreateDirectory(Path.Combine(d,"OptiScaler"));
            foreach(string name in new[]{Enabler,Nukem}) File.WriteAllText(FsrTarget(d,name),"first original "+name);
            var options=Options(exe); options.FsrDirectory=sources; options.InstallFsrEnabler=true; options.InstallFsrNukem=true;
            Install(Zip(d,"one",null),options,"one"); var first=InstallerEngine.ReadManifest(exe);
            foreach(string name in new[]{Enabler,Nukem}) File.WriteAllBytes(Path.Combine(sources,name),MarkedPe(60));
            Install(Zip(d,"two",null),options,"two"); var second=InstallerEngine.ReadManifest(exe);
            foreach(string name in new[]{Enabler,Nukem}) { var baseline=first.Files.Find(f=>f.Path=="OptiScaler\\"+name); var current=second.Files.Find(f=>f.Path==baseline.Path); Assert(baseline.HadOriginal&&current.HadOriginal&&baseline.BackupName==current.BackupName&&baseline.OriginalHash==current.OriginalHash,"first FSR baseline replaced"); Assert(File.ReadAllText(Path.Combine(d,InstallerEngine.StateDirectoryName,"originals",current.BackupName))=="first original "+name,"first FSR backup bytes changed"); AssertBytes(FsrTarget(d,name),MarkedPe(60),"FSR update bytes missing"); }
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);
            foreach(string name in new[]{Enabler,Nukem}) Assert(File.ReadAllText(FsrTarget(d,name))=="first original "+name,"original FSR not restored");
        });
        Run("selected FSR requires a source directory",delegate {
            foreach(string directory in new[]{null,Path.Combine(root,"missing FSR folder")}) {
                string d=Dir(),exe=Game(d); var options=Options(exe); options.InstallFsrEnabler=true; options.FsrDirectory=directory;
                Reject(delegate{Install(Zip(d,"one",null),options,"one");}); AssertNoInstall(d);
            }
        });
        for(int invalid=0;invalid<4;invalid++)
        {
            int invalidSource=invalid;
            Run("invalid selected FSR source is rejected before writes kind "+invalidSource,delegate {
                foreach(string name in new[]{Enabler,Nukem}) {
                    string d=Dir(),exe=Game(d),sources=Dir(); var options=Options(exe); options.FsrDirectory=sources; options.InstallFsrEnabler=name==Enabler; options.InstallFsrNukem=name==Nukem;
                    if(invalidSource!=0) { byte[] bytes=invalidSource==2?Pe(false):invalidSource==3?Encoding.UTF8.GetBytes("not a DLL"):Pe(true); if(invalidSource==1) { bytes[132]=0x4c; bytes[133]=1; } File.WriteAllBytes(Path.Combine(sources,name),bytes); }
                    Reject(delegate{Install(Zip(d,"one",null),options,"one");}); AssertNoInstall(d);
                }
            });
        }
        Run("invalid second FSR source leaves previous installation unchanged",delegate {
            string d=Dir(),exe=Game(d),sources=FsrSources(true,true,70); Install(Zip(d,"one",null),Options(exe),"one");
            string manifestPath=Path.Combine(d,InstallerEngine.StateDirectoryName,"manifest.json"), previousManifest=File.ReadAllText(manifestPath), previousIni=File.ReadAllText(Path.Combine(d,"OptiScaler.ini"));
            File.WriteAllBytes(Path.Combine(sources,Nukem),Pe(false)); var options=Options(exe); options.FsrDirectory=sources; options.InstallFsrEnabler=true; options.InstallFsrNukem=true;
            Reject(delegate{Install(Zip(d,"two",null),options,"two");});
            Assert(File.ReadAllText(manifestPath)==previousManifest&&File.ReadAllText(Path.Combine(d,"OptiScaler.ini"))==previousIni,"failed FSR update changed manifest or INI");
            Assert(File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="one","failed FSR update changed runtime"); Assert(!File.Exists(FsrTarget(d,Enabler))&&!File.Exists(FsrTarget(d,Nukem)),"partial FSR commit");
        });
        Run("DLSSG required name contract returns an independent allowlist",delegate {
            string[] names=InstallerEngine.GetDlssgRequiredFileNames();
            Assert(String.Join("|",names)==String.Join("|",DlssgRequired),"DLSSG required allowlist differs from loader contract");
            names[0]="tampered.dll"; Assert(InstallerEngine.GetDlssgRequiredFileNames()[0]==DlssgRequired[0],"caller can modify engine allowlist");
        });
        for(int licenses=0;licenses<4;licenses++)
        {
            int selectedLicenses=licenses;
            Run("DLSSG deploys to game root with community extras mask "+selectedLicenses,delegate {
                string d=Dir(),exe=Game(d),sources=DlssgSources(selectedLicenses,80); var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=sources;
                for(int i=0;i<DlssgExtraDlls.Length;i++) File.WriteAllBytes(Path.Combine(sources,DlssgExtraDlls[i]),MarkedPe((byte)(81+i)));
                File.WriteAllText(Path.Combine(sources,DlssgExtraLicenses[0]),"nis license "+selectedLicenses);
                foreach(string name in new[]{"unexpected.dll","helper.exe"}) File.WriteAllText(Path.Combine(sources,name),"must not copy");
                Directory.CreateDirectory(Path.Combine(sources,"nested")); File.WriteAllText(Path.Combine(sources,"nested","extra.dll"),"must not copy nested folders");
                File.WriteAllText(Path.Combine(d,"OptiScaler.ini"),"[FrameGen]\nEnabled=false\nFGType=keep-choice\n[DLSSG]\nMode=manual\n");
                Install(Zip(d,"one",null),options,"one"); var manifest=InstallerEngine.ReadManifest(exe);
                foreach(string name in DlssgRequired) { AssertBytes(DlssgTarget(d,name),File.ReadAllBytes(Path.Combine(sources,name)),"required DLSSG copy missing or changed: "+name); var record=manifest.Files.Find(f=>f.Path==DlssgRelative(name)); Assert(record!=null&&!record.HadOriginal,"new DLSSG record missing or adopted original"); }
                foreach(string name in DlssgExtraDlls) AssertBytes(DlssgTarget(d,name),File.ReadAllBytes(Path.Combine(sources,name)),"community extra DLL missing: "+name);
                Assert(File.ReadAllText(DlssgTarget(d,DlssgExtraLicenses[0]))=="nis license "+selectedLicenses,"nis license not copied");
                int expected=DlssgRequired.Length+DlssgExtraDlls.Length+DlssgExtraLicenses.Length;
                for(int i=0;i<DlssgLicenses.Length;i++) { bool present=(selectedLicenses&(1<<i))!=0; Assert(File.Exists(DlssgTarget(d,DlssgLicenses[i]))==present,"wrong optional DLSSG license selection"); if(present) { expected++; AssertBytes(DlssgTarget(d,DlssgLicenses[i]),File.ReadAllBytes(Path.Combine(sources,DlssgLicenses[i])),"DLSSG license bytes changed"); Assert(manifest.Files.Exists(f=>f.Path==DlssgRelative(DlssgLicenses[i])),"DLSSG license missing from manifest"); } }
                Assert(Directory.Exists(Path.Combine(d,"OptiScaler","streamline")),"OptiScaler\\streamline not created");
                Assert(!File.Exists(Path.Combine(d,"unexpected.dll"))&&!File.Exists(Path.Combine(d,"helper.exe"))&&!File.Exists(Path.Combine(d,"nested","extra.dll")),"DLSSG copied files outside allowlist");
                Assert(!File.Exists(Path.Combine(d,"sl.interposer.dll")),"DLSSG spilled to game root");
                string slDir=Path.Combine(d,"OptiScaler","streamline");
                int slAllowlist=0; foreach(string name in Directory.GetFiles(slDir)) if(DlssgRequired.Concat(DlssgExtraDlls).Concat(DlssgExtraLicenses).Concat(DlssgLicenses).Contains(Path.GetFileName(name),StringComparer.OrdinalIgnoreCase)) slAllowlist++;
                Assert(slAllowlist==expected,"DLSSG OptiScaler\\streamline allowlist count mismatch");
                Assert(File.ReadAllText(Path.Combine(d,"OptiScaler.ini")).Contains("[FrameGen]\r\nEnabled=false\r\nFGType=keep-choice\r\n[DLSSG]\r\nMode=manual"),"FrameGen or DLSSG settings changed");
                InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);
                foreach(string name in DlssgRequired.Concat(DlssgExtraDlls).Concat(DlssgExtraLicenses).Concat(DlssgLicenses)) Assert(!File.Exists(DlssgTarget(d,name)),"OptiScaler\\streamline retained after uninstall: "+name);
                foreach(string name in DlssgRequired) Assert(File.Exists(Path.Combine(sources,name)),"source DLSSG file removed");
            });
        }
        Run("DLSSG optional extras are skipped when absent from source",delegate {
            string d=Dir(),exe=Game(d),sources=DlssgSources(0,85); var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=sources;
            Install(Zip(d,"one",null),options,"one");
            foreach(string name in DlssgExtraDlls.Concat(DlssgExtraLicenses)) Assert(!File.Exists(DlssgTarget(d,name)),"absent extra was invented: "+name);
            foreach(string name in DlssgRequired) Assert(File.Exists(DlssgTarget(d,name)),"required missing without extras: "+name);
        });
        Run("DLSSG accepts SDK-style bin/x64 layout",delegate {
            string d=Dir(),exe=Game(d),sdk=Dir();
            Directory.CreateDirectory(Path.Combine(sdk,"bin","x64"));
            foreach(string name in DlssgRequired) File.WriteAllBytes(Path.Combine(sdk,"bin","x64",name),MarkedPe(230));
            File.WriteAllText(Path.Combine(sdk,"license.txt"),"nvidia sdk license");
            File.WriteAllText(Path.Combine(sdk,"3rd-party-licenses.md"),"third party");
            var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=sdk;
            Install(Zip(d,"one",null),options,"v2.14.1");
            foreach(string name in DlssgRequired) AssertBytes(DlssgTarget(d,name),MarkedPe(230),"SDK layout required missing: "+name);
            Assert(File.Exists(Path.Combine(d,"OptiScaler","streamline","Streamline-LICENSE.txt")),"SDK license.txt not mapped");
            Assert(File.Exists(Path.Combine(d,"OptiScaler","streamline","Streamline-3rd-party-licenses.md")),"3rd-party license not mapped");
        });
        foreach(string required in DlssgRequired)
        {
            string missing=required;
            Run("DLSSG missing required source rejected: "+missing,delegate {
                string d=Dir(),exe=Game(d),sources=DlssgSources(3,90); File.Delete(Path.Combine(sources,missing));
                var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=sources;
                Reject(delegate{Install(Zip(d,"one",null),options,"one");}); AssertNoInstall(d);
            });
        }
        Run("selected DLSSG requires a source directory",delegate {
            foreach(string directory in new[]{null,Path.Combine(root,"missing Streamline folder")}) { string d=Dir(),exe=Game(d); var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=directory; Reject(delegate{Install(Zip(d,"one",null),options,"one");}); AssertNoInstall(d); }
        });
        for(int invalid=0;invalid<3;invalid++)
        {
            int invalidSource=invalid;
            Run("DLSSG rejects every invalid DLL before writes kind "+invalidSource,delegate {
                foreach(string name in DlssgRequired) {
                    string d=Dir(),exe=Game(d),sources=DlssgSources(3,100); byte[] bytes=invalidSource==0?Encoding.UTF8.GetBytes("not PE"):invalidSource==1?Pe(true):Pe(false);
                    if(invalidSource==1) { bytes[132]=0x4c; bytes[133]=1; } File.WriteAllBytes(Path.Combine(sources,name),bytes);
                    var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=sources; Reject(delegate{Install(Zip(d,"one",null),options,"one");}); AssertNoInstall(d);
                }
            });
        }
        Run("unchecked DLSSG and FSR survive ZIP updates together",delegate {
            string d=Dir(),exe=Game(d),sources=DlssgSources(3,110),fsr=FsrSources(true,true,120); var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=sources; options.InstallFsrEnabler=true; options.InstallFsrNukem=true; options.FsrDirectory=fsr;
            Install(Zip(d,"one",null),options,"one"); var before=InstallerEngine.ReadManifest(exe);
            string zip=Zip(d,"two",null); using(var archive=ZipFile.Open(zip,ZipArchiveMode.Update)) { foreach(string name in DlssgRequired) Put(archive,DlssgRelative(name),MarkedPe(130)); foreach(string name in DlssgLicenses) Put(archive,DlssgRelative(name),Encoding.UTF8.GetBytes("different ZIP license")); }
            var uncheckedOptions=Options(exe); uncheckedOptions.StreamlineDirectory=Path.Combine(d,"unused missing Streamline"); uncheckedOptions.FsrDirectory=Path.Combine(d,"unused missing FSR"); Install(zip,uncheckedOptions,"two"); var after=InstallerEngine.ReadManifest(exe);
            Assert(after.Version=="two"&&File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="two","normal update did not complete");
            foreach(var first in before.Files.FindAll(f=>DlssgRequired.Concat(DlssgLicenses).Select(n=>DlssgRelative(n)).Contains(f.Path,StringComparer.OrdinalIgnoreCase)||f.Path=="OptiScaler\\"+Enabler||f.Path=="OptiScaler\\"+Nukem)) { var kept=after.Files.Find(f=>f.Path==first.Path); Assert(kept!=null&&kept.InstalledHash==first.InstalledHash&&kept.HadOriginal==first.HadOriginal,"unchecked optional record changed"); }
            foreach(string name in DlssgRequired) AssertBytes(DlssgTarget(d,name),File.ReadAllBytes(Path.Combine(sources,name)),"unchecked DLSSG overwritten from ZIP");
            foreach(string name in DlssgLicenses) AssertBytes(DlssgTarget(d,name),File.ReadAllBytes(Path.Combine(sources,name)),"unchecked license overwritten from ZIP");
            foreach(string name in new[]{Enabler,Nukem}) AssertBytes(FsrTarget(d,name),File.ReadAllBytes(Path.Combine(fsr,name)),"unchecked FSR bytes changed");
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); Assert(!Directory.Exists(Path.Combine(d,"OptiScaler")),"optional runtimes retained after uninstall");
            foreach(string name in DlssgRequired.Concat(DlssgLicenses)) Assert(!File.Exists(DlssgTarget(d,name)),"uninstall left Streamline in OptiScaler\\streamline: "+name);
        });
        Run("unchecked unmanaged DLSSG is not adopted from game or ZIP",delegate {
            string d=Dir(),exe=Game(d),sources=DlssgSources(3,140);
            Directory.CreateDirectory(Path.Combine(d,"OptiScaler","streamline"));
            foreach(string name in DlssgRequired.Concat(DlssgLicenses)) File.WriteAllText(DlssgTarget(d,name),"original "+name);
            string zip=Zip(d,"one",null); using(var archive=ZipFile.Open(zip,ZipArchiveMode.Update)) foreach(string name in Directory.GetFiles(sources)) Put(archive,DlssgRelative(Path.GetFileName(name)),File.ReadAllBytes(name));
            Install(zip,Options(exe),"one"); Assert(!InstallerEngine.ReadManifest(exe).Files.Exists(f=>DlssgRequired.Concat(DlssgLicenses).Select(n=>DlssgRelative(n)).Contains(f.Path,StringComparer.OrdinalIgnoreCase)),"unchecked Streamline adopted");
            Install(Zip(d,"two",null),Options(exe),"two"); InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);
            foreach(string name in Directory.GetFiles(sources)) Assert(File.ReadAllText(DlssgTarget(d,Path.GetFileName(name)))=="original "+Path.GetFileName(name),"unmanaged Streamline overwritten or deleted");
        });
        Run("DLSSG checked update keeps first DLL and license backups",delegate {
            string d=Dir(),exe=Game(d),sources=DlssgSources(3,150);
            Directory.CreateDirectory(Path.Combine(d,"OptiScaler","streamline"));
            foreach(string path in Directory.GetFiles(sources)) File.WriteAllText(DlssgTarget(d,Path.GetFileName(path)),"first original "+Path.GetFileName(path));
            var options=Options(exe); options.InstallDlssg=true; options.StreamlineDirectory=sources; Install(Zip(d,"one",null),options,"one"); var first=InstallerEngine.ReadManifest(exe);
            foreach(string name in DlssgRequired) File.WriteAllBytes(Path.Combine(sources,name),MarkedPe(160)); foreach(string name in DlssgLicenses) File.WriteAllText(Path.Combine(sources,name),"updated license "+name);
            Install(Zip(d,"two",null),options,"two"); var second=InstallerEngine.ReadManifest(exe);
            foreach(string source in Directory.GetFiles(sources)) { string name=Path.GetFileName(source); var original=first.Files.Find(f=>f.Path==DlssgRelative(name)); var current=second.Files.Find(f=>f.Path==original.Path); Assert(original.HadOriginal&&current.HadOriginal&&current.BackupName==original.BackupName&&current.OriginalHash==original.OriginalHash,"first Streamline backup replaced"); Assert(File.ReadAllText(Path.Combine(d,InstallerEngine.StateDirectoryName,"originals",current.BackupName))=="first original "+name,"Streamline backup bytes changed"); AssertBytes(DlssgTarget(d,name),File.ReadAllBytes(source),"Streamline update failed"); }
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None); foreach(string source in Directory.GetFiles(sources)) Assert(File.ReadAllText(DlssgTarget(d,Path.GetFileName(source)))=="first original "+Path.GetFileName(source),"Streamline original not restored");
        });
        Run("invalid DLSSG update leaves existing FSR and runtime unchanged",delegate {
            string d=Dir(),exe=Game(d),fsr=FsrSources(true,true,170); var firstOptions=Options(exe); firstOptions.InstallFsrEnabler=true; firstOptions.InstallFsrNukem=true; firstOptions.FsrDirectory=fsr; Install(Zip(d,"one",null),firstOptions,"one");
            string previousManifest=File.ReadAllText(Path.Combine(d,InstallerEngine.StateDirectoryName,"manifest.json")), previousIni=File.ReadAllText(Path.Combine(d,"OptiScaler.ini")); byte[] previousFsr=File.ReadAllBytes(FsrTarget(d,Enabler));
            string sources=DlssgSources(3,180); File.WriteAllBytes(Path.Combine(sources,"sl.pcl.dll"),Pe(false)); File.WriteAllBytes(Path.Combine(fsr,Enabler),MarkedPe(190)); firstOptions.InstallDlssg=true; firstOptions.StreamlineDirectory=sources;
            Reject(delegate{Install(Zip(d,"two",null),firstOptions,"two");});
            Assert(File.ReadAllText(Path.Combine(d,InstallerEngine.StateDirectoryName,"manifest.json"))==previousManifest&&File.ReadAllText(Path.Combine(d,"OptiScaler.ini"))==previousIni,"failed DLSSG update changed manifest or INI");
            AssertBytes(FsrTarget(d,Enabler),previousFsr,"failed DLSSG update partially changed FSR"); Assert(File.ReadAllText(Path.Combine(d,"OptiScaler","component.dll"))=="one","failed DLSSG update changed runtime");
            foreach(string name in DlssgRequired) Assert(!File.Exists(DlssgTarget(d,name)),"failed DLSSG update partially committed OptiScaler\\streamline: "+name);
        });
        if(args.Length>=2&&args[0]=="--official-zip") Run("official release installs all runtime assets and restores baseline",delegate {
            string d=Path.Combine(Dir(),"中文游戏 [测试]" ); Directory.CreateDirectory(d); string exe=Game(d), ini=Path.Combine(d,"OptiScaler.ini");
            File.WriteAllText(ini,"[MySettings]\nKeep=yes\n"); File.WriteAllText(Path.Combine(d,"user-save.dat"),"my game save");
            var options=Options(exe); bool realModel=args.Length==4&&args[2]=="--model";
            if(realModel) { options.ModelDll=args[3]; options.EnableNeuralRendering=true; }
            Install(args[1],options,"v0.2.0-dlssnr"); var manifest=InstallerEngine.ReadManifest(exe);
            Assert(manifest.Files.Count==(realModel?18:17),"runtime/documentation file count mismatch");
            if(realModel) { Assert(new FileInfo(Path.Combine(d,"nvngx_dlssnr.dll")).Length==new FileInfo(args[3]).Length,"real model copy incomplete"); Assert(File.ReadAllText(ini).Contains("Enabled=true"),"NR not enabled with real model"); }
            Assert(new FileInfo(Path.Combine(d,"OptiScaler","libxess.dll")).Length==77795704,"official dependency missing");
            Assert(new FileInfo(Path.Combine(d,"nvngx.dll_dlssnr.dll")).Length==114688,"official forwarder missing");
            Assert(File.ReadAllText(ini).Contains("Keep=yes"),"existing settings lost");
            InstallerEngine.Uninstall(exe,delegate{},CancellationToken.None);
            Assert(File.ReadAllText(ini)=="[MySettings]\nKeep=yes\n","original INI not restored"); Assert(File.ReadAllText(Path.Combine(d,"user-save.dat"))=="my game save","unrelated save modified"); Assert(!Directory.Exists(Path.Combine(d,"OptiScaler")),"runtime directory retained"); Assert(!File.Exists(Path.Combine(d,"nvngx_dlssnr.dll")),"model copy retained after uninstall");
        });
        Console.WriteLine("Failures: " + failed + "; fixtures: " + root); return failed==0 ? 0 : 1;
    }
}
