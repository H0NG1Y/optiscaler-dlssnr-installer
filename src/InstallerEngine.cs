using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace OptiScalerInstaller
{
    public static class InstallerEngine
    {
        public const string StateDirectoryName = ".optiscaler-dlssnr-installer";
        const string Owner = "OptiScaler DLSSNR Installer / state v1";
        static readonly string[] Proxies = { "dxgi.dll", "winmm.dll", "version.dll", "dbghelp.dll", "d3d12.dll", "wininet.dll", "winhttp.dll", "OptiScaler.asi" };
        static readonly string[] FsrPaths = { "OptiScaler\\dlss-enabler-headless.dll", "OptiScaler\\dlssg_to_fsr3_amd_is_better.dll" };
        // OptiScaler's own FG runtime — NVIDIA Streamline SDK bin/x64 → game\OptiScaler\streamline\
        static readonly string[] DlssgRequiredNames = { "sl.interposer.dll", "sl.common.dll", "nvngx_dlssg.dll", "sl.dlss_g.dll", "sl.reflex.dll", "sl.pcl.dll" };
        static readonly string[] DlssgExtraNames = { "nvngx_dlss.dll", "sl.dlss.dll", "sl.dlss_nr.dll", "sl.nis.dll", "nis.license.txt" };
        static readonly string[] DlssgLicenseNames = { "reflex.license.txt", "nvngx_dlss.license.txt", "Streamline-LICENSE.txt", "Streamline-3rd-party-licenses.md" };
        static readonly string[] DlssgTargetPaths = DlssgRequiredNames.Concat(DlssgExtraNames).Concat(DlssgLicenseNames)
            .Select(name => "OptiScaler\\streamline\\" + name).ToArray();
        // v1.2/v1.3 game-root placements — preserve when unchecked, never re-deploy from ZIP.
        static readonly string[] DlssgLegacyRootPaths = DlssgRequiredNames.Concat(new[] { "reflex.license.txt", "nvngx_dlss.license.txt" }).ToArray();
        static readonly string[] OptionalPaths = FsrPaths.Concat(DlssgTargetPaths).Concat(DlssgLegacyRootPaths).ToArray();
        public static string[] GetDlssgRequiredFileNames() { return (string[])DlssgRequiredNames.Clone(); }
        public sealed class Change { public string Path; public string BeforeHash; public string ExpectedBeforeHash; public string AfterHash; public string Backup; public string Source; }
        public sealed class Journal { public int Schema = 1; public string Root; public List<Change> Changes = new List<Change>(); }
        sealed class HeldMutex : IDisposable
        {
            Mutex mutex;
            public HeldMutex(string root)
            {
                mutex = new Mutex(false, "Local\\OptiScalerInstaller_" + HashBytes(Encoding.UTF8.GetBytes(root.ToUpperInvariant())));
                bool held; try { held = mutex.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                if (!held) { mutex.Dispose(); throw new InvalidOperationException("另一个安装器正在处理此游戏，请等待其完成。"); }
            }
            public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
        }
        static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }; }
        static string HashBytes(byte[] bytes) { using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static string Hash(string file) { using(var s=File.OpenRead(file)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
        static bool IsHash(string value) { return value != null && Regex.IsMatch(value, "^[a-f0-9]{64}$"); }
        static string ExistingHash(string file) { return File.Exists(file) ? Hash(file) : null; }
        static bool Equal(string a,string b) { return String.Equals(a,b,StringComparison.OrdinalIgnoreCase); }
        static void NoLinks(string path)
        {
            string p=Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(p))
            {
                if ((Directory.Exists(p)||File.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new IOException("不支持符号链接或目录联接，请选择真实路径："+p);
                var parent=Path.GetDirectoryName(p); if(parent==p) break; p=parent;
            }
        }
        static string Relative(string relative)
        {
            if (String.IsNullOrWhiteSpace(relative)) throw new InvalidOperationException("空文件路径。");
            string r=relative.Replace('/', '\\');
            if (Path.IsPathRooted(r) || r.IndexOf(':')>=0) throw new InvalidOperationException("拒绝绝对路径或替代数据流："+relative);
            foreach(string s in r.Split('\\'))
                if(s.Length==0||s=="."||s==".."||s.EndsWith(".")||s.EndsWith(" ")||s.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(s,"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])($|\\.)",RegexOptions.IgnoreCase)) throw new InvalidOperationException("无效的文件路径："+relative);
            return r;
        }
        static string At(string root,string relative)
        {
            string r=Relative(relative), full=Path.GetFullPath(Path.Combine(root,r));
            if(!full.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)) throw new IOException("文件路径超出目标文件夹。");
            NoLinks(full); return full;
        }
        static void ManagedPath(string path)
        {
            string r=Relative(path);
            if(r.Split('\\')[0].StartsWith(StateDirectoryName,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("压缩包/记录试图修改安装器自身数据。");
            string ext=Path.GetExtension(r).ToLowerInvariant();
            if(ext!=".dll"&&ext!=".asi"&&ext!=".ini"&&ext!=".txt"&&ext!=".md") throw new InvalidOperationException("非支持的安装文件："+path);
        }
        static string Root(string gameExe)
        {
            if(String.IsNullOrWhiteSpace(gameExe)||!File.Exists(gameExe)||!Equal(Path.GetExtension(gameExe),".exe")) throw new InvalidOperationException("请选择实际游戏的 EXE 文件。");
            string exe=Path.GetFullPath(gameExe); NoLinks(exe); ValidatePe(exe,false);
            string root=Path.GetDirectoryName(exe), windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if(Equal(root,Path.GetPathRoot(root))||Equal(root,windows)||root.StartsWith(windows+"\\",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("不能将系统目录或磁盘根目录作为游戏目录。");
            return root;
        }
        public static void ValidatePe(string path,bool dll)
        {
            using(var s=File.OpenRead(path)) using(var r=new BinaryReader(s))
            {
                if(s.Length<256||r.ReadUInt16()!=0x5a4d) throw new InvalidOperationException("不是有效的 Windows 程序："+path);
                s.Position=60; int pe=r.ReadInt32();
                if(pe<64||pe>s.Length-26) throw new InvalidOperationException("PE 文件头损坏："+path);
                s.Position=pe;
                if(r.ReadUInt32()!=0x4550||r.ReadUInt16()!=0x8664) throw new InvalidOperationException("只支持 x64 游戏和 x64 模型 DLL："+path);
                s.Position=pe+22; bool isDll=(r.ReadUInt16()&0x2000)!=0;
                if(isDll!=dll || r.ReadUInt16()!=0x20b) throw new InvalidOperationException("文件类型不匹配："+path);
            }
        }
        static void GameStopped(string gameExe)
        {
            foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(gameExe))) using(process)
            {
                try { if(Equal(process.MainModule.FileName,Path.GetFullPath(gameExe))) throw new InvalidOperationException("请先关闭游戏，再进行安装或卸载。"); }
                catch(System.ComponentModel.Win32Exception) { throw new InvalidOperationException("检测到同名进程且无法确认路径，请关闭游戏后重试。"); }
            }
        }
        static string State(string root,bool create)
        {
            string state=At(root,StateDirectoryName), marker=At(state,"owner.txt");
            if(Directory.Exists(state)) { if(!File.Exists(marker)||File.ReadAllText(marker)!=Owner) throw new InvalidOperationException("安装记录文件夹已存在但无法识别，请检查："+state); }
            else if(create) { Directory.CreateDirectory(state); File.WriteAllText(marker,Owner,Encoding.UTF8); }
            return state;
        }
        static void NoPending(string state) { if(Directory.Exists(At(state,"pending"))) throw new InvalidOperationException("检测到未完成的事务，请先点击“恢复未完成操作”。"); }
        static InstallManifest Load(string root,string state)
        {
            string path=At(state,"manifest.json"); if(!File.Exists(path)) return null;
            InstallManifest m;
            try { m=Json().Deserialize<InstallManifest>(File.ReadAllText(path)); } catch(Exception e) { throw new InvalidOperationException("安装清单损坏，不能继续覆盖。",e); }
            if(m==null||m.Schema!=1||!Equal(m.Root,root)||m.Files==null||m.Files.Count>4096||!Proxies.Any(p=>Equal(p,m.ProxyName))) throw new InvalidOperationException("安装清单不属于当前游戏或版本不兼容。");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var f in m.Files)
            {
                ManagedPath(f.Path); At(root,f.Path);
                if(!seen.Add(Relative(f.Path))||!IsHash(f.InstalledHash)||f.HadOriginal&&(!IsHash(f.OriginalHash)||!Regex.IsMatch(f.BackupName??"","^[a-f0-9]{32}\\.bak$"))) throw new InvalidOperationException("安装清单中的文件记录无效。");
            }
            if(m.CreatedDirectories==null) throw new InvalidOperationException("安装清单缺少目录记录。");
            foreach(string d in m.CreatedDirectories) { Relative(d); At(root,d); if(d.StartsWith(StateDirectoryName,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("目录记录无效。"); }
            return m;
        }
        public static InstallManifest ReadManifest(string gameExe) { string root=Root(gameExe); using(new HeldMutex(root)) { return Load(root,State(root,false)); } }
        static void CheckOriginals(InstallManifest m,string state)
        {
            if(m==null) return;
            foreach(var f in m.Files.Where(x=>x.HadOriginal)) { string p=At(state,"originals\\"+f.BackupName); if(!File.Exists(p)||!Equal(Hash(p),f.OriginalHash)) throw new InvalidOperationException("原始备份缺失或已损坏，已停止操作："+f.Path); }
        }
        static void CheckCurrent(InstallManifest m,string root)
        {
            if(m==null) return;
            foreach(var f in m.Files) { string p=At(root,f.Path); if(!Equal(f.Path,"OptiScaler.ini")&&File.Exists(p)&&!Equal(Hash(p),f.InstalledHash)) throw new InvalidOperationException("安装后此文件被其他程序或用户修改，已保留。请处理冲突后重试："+f.Path); }
        }
        static Dictionary<string,string> GetFsrSources(InstallOptions options,CancellationToken token)
        {
            var sources=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if(!options.InstallFsrEnabler&&!options.InstallFsrNukem) return sources;
            if(String.IsNullOrWhiteSpace(options.FsrDirectory)) throw new InvalidOperationException("勾选 FSR 组件后，请选择包含对应 DLL 的文件夹。");
            string directory=Path.GetFullPath(options.FsrDirectory); NoLinks(directory);
            if(!Directory.Exists(directory)) throw new InvalidOperationException("FSR 文件夹不存在："+directory);
            for(int i=0;i<FsrPaths.Length;i++)
            {
                if(i==0?!options.InstallFsrEnabler:!options.InstallFsrNukem) continue;
                token.ThrowIfCancellationRequested(); string source=At(directory,Path.GetFileName(FsrPaths[i]));
                if(!File.Exists(source)) throw new InvalidOperationException("FSR 文件夹中缺少所选组件："+Path.GetFileName(source));
                ValidatePe(source,true); sources.Add(FsrPaths[i],source);
            }
            return sources;
        }
        static Dictionary<string,string> GetDlssgSources(InstallOptions options,CancellationToken token)
        {
            var sources=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if(!options.InstallDlssg) return sources;
            if(String.IsNullOrWhiteSpace(options.StreamlineDirectory)) throw new InvalidOperationException("勾选 Streamline 后，请选择包含 SDK 运行库的文件夹，或使用官方 SDK 下载。");
            string directory=Path.GetFullPath(options.StreamlineDirectory); NoLinks(directory);
            if(!Directory.Exists(directory)) throw new InvalidOperationException("Streamline 文件夹不存在："+directory);
            // Accept either flat folder or an extracted SDK tree (…/bin/x64/…).
            string binX64=At(directory,Path.Combine("bin","x64"));
            string searchRoot=Directory.Exists(binX64)?binX64:directory;
            foreach(string name in DlssgRequiredNames)
            {
                token.ThrowIfCancellationRequested(); string source=At(searchRoot,name);
                if(!File.Exists(source)) throw new InvalidOperationException("Streamline 目录中缺少 DLSSG 必需组件："+name);
                ValidatePe(source,true); sources.Add("OptiScaler\\streamline\\"+name,source);
            }
            foreach(string name in DlssgExtraNames)
            {
                token.ThrowIfCancellationRequested(); string source=At(searchRoot,name);
                if(!File.Exists(source)) continue;
                if(Equal(Path.GetExtension(name),".dll")) ValidatePe(source,true);
                sources.Add("OptiScaler\\streamline\\"+name,source);
            }
            foreach(string name in DlssgLicenseNames)
            {
                token.ThrowIfCancellationRequested();
                string source=At(searchRoot,name);
                if(!File.Exists(source))
                {
                    // SDK layout: license.txt / 3rd-party-licenses.md at archive root; bin/x64 for nvngx/reflex licenses.
                    string rootFile=At(directory,name);
                    string alt=name=="Streamline-LICENSE.txt"?At(directory,"license.txt")
                        :name=="Streamline-3rd-party-licenses.md"?At(directory,"3rd-party-licenses.md"):null;
                    if(File.Exists(rootFile)) source=rootFile;
                    else if(alt!=null&&File.Exists(alt)) source=alt;
                }
                if(File.Exists(source)) sources.Add("OptiScaler\\streamline\\"+name,source);
            }
            return sources;
        }
        static Dictionary<string,string> Extract(string zipPath,string stage,CancellationToken token)
        {
            var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total=0;
            using(var z=ZipFile.OpenRead(zipPath))
            {
                if(z.Entries.Count>4096) throw new InvalidOperationException("压缩包文件数量异常。");
                foreach(var entry in z.Entries)
                {
                    token.ThrowIfCancellationRequested(); string raw=entry.FullName.Replace('/','\\'); bool directory=raw.EndsWith("\\"); string rel=Relative(directory ? raw.TrimEnd('\\') : raw), target=At(stage,rel);
                    if(!seen.Add(rel)) throw new InvalidOperationException("压缩包中有重复文件路径："+rel);
                    if((entry.ExternalAttributes&0x400)!=0 || ((entry.ExternalAttributes>>16)&0xf000)==0xa000) throw new InvalidOperationException("压缩包不能包含链接。");
                    if(directory) continue;
                    total=checked(total+entry.Length); if(total>1024L*1024*1024) throw new InvalidOperationException("压缩包展开超过 1 GB。");
                    string ext=Path.GetExtension(rel).ToLowerInvariant();
                    // Community packages ship docs/images/scripts. Extract only installable assets;
                    // skip the rest instead of rejecting the whole ZIP.
                    if(ext!=".dll"&&ext!=".asi"&&ext!=".ini"&&ext!=".txt"&&ext!=".md") continue;
                    ManagedPath(rel); Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using(var input=entry.Open()) using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write))
                    {
                        byte[] buffer=new byte[128*1024]; int count; long actual=0;
                        while((count=input.Read(buffer,0,buffer.Length))>0) { token.ThrowIfCancellationRequested(); actual+=count; if(actual>entry.Length) throw new InvalidOperationException("压缩包长度不符。"); output.Write(buffer,0,count); }
                        if(actual!=entry.Length) throw new InvalidOperationException("压缩包内容不完整。");
                    }
                    files.Add(rel,target);
                }
            }
            foreach(string required in new[]{"OptiScaler.dll","OptiScaler.ini"}) if(!files.ContainsKey(required)) throw new InvalidOperationException("请选择完整的 OptiScaler DLSSNR 官方 ZIP，缺少："+required);
            ValidatePe(files["OptiScaler.dll"],true);
            if(files.ContainsKey("nvngx.dll_dlssnr.dll")) ValidatePe(files["nvngx.dll_dlssnr.dll"],true);
            if(files.ContainsKey("nvngx_dlssnr.dll")) throw new InvalidOperationException("官方 ZIP 不包含模型 DLL，请单独选择模型，勿使用重新打包的安装包。");
            return files;
        }
        static void FillBridgeDll(Dictionary<string,string> files,string stage,InstallOptions options,Action<string> log)
        {
            if(files.ContainsKey("nvngx.dll_dlssnr.dll")) return;
            string fallback=options==null||String.IsNullOrWhiteSpace(options.BridgeDllFallback)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"nvngx.dll_dlssnr.dll")
                : options.BridgeDllFallback;
            fallback=Path.GetFullPath(fallback);
            if(!File.Exists(fallback)||!Equal(Path.GetFileName(fallback),"nvngx.dll_dlssnr.dll"))
                throw new InvalidOperationException("发布包缺少 nvngx.dll_dlssnr.dll 桥接文件；请在安装器目录放置该文件后重试，或改用 Dagherbou 官方包。");
            NoLinks(fallback); ValidatePe(fallback,true);
            string staged=At(stage,"bridge-nvngx.dll_dlssnr.dll");
            File.Copy(fallback,staged,false); ValidatePe(staged,true);
            files.Add("nvngx.dll_dlssnr.dll",staged);
            if(log!=null) log("发布包未含 nvngx.dll_dlssnr.dll，已改用安装器目录中的桥接文件。");
        }
        static string SetIni(string input,string section,string key,string value)
        {
            var lines=new List<string>(input.Replace("\r\n","\n").Replace('\r','\n').Split('\n')); bool inSection=false,foundSection=false,foundKey=false; int insert=lines.Count;
            for(int i=0;i<lines.Count;i++)
            {
                string line=lines[i].Trim();
                if(line.StartsWith("[")&&line.EndsWith("]")) { if(inSection) insert=i; inSection=Equal(line,"["+section+"]"); if(inSection) { foundSection=true; insert=i+1; } }
                else if(inSection) { insert=i+1; int eq=line.IndexOf('='); if(eq>0&&Equal(line.Substring(0,eq).Trim(),key)) { lines[i]=key+"="+value; foundKey=true; } }
            }
            if(!foundSection) { lines.Add("["+section+"]"); lines.Add(key+"="+value); } else if(!foundKey) lines.Insert(insert,key+"="+value);
            return String.Join("\r\n",lines);
        }
        static void EnsureParents(string root,string path,List<string> created)
        {
            string parent=Path.GetDirectoryName(path); var stack=new Stack<string>();
            while(!Equal(parent,root)&&!Directory.Exists(parent)) { stack.Push(parent); parent=Path.GetDirectoryName(parent); }
            while(stack.Count>0) { string p=stack.Pop(); NoLinks(p); Directory.CreateDirectory(p); string r=p.Substring(root.Length+1); if(!created.Contains(r,StringComparer.OrdinalIgnoreCase)) created.Add(r); }
        }
        static void SaveJson(string path,object obj) { File.WriteAllText(path,Json().Serialize(obj),new UTF8Encoding(false)); }
        static void AtomicCopy(string source,string target,string root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string relative=target.Substring(root.Length+1)+".opti-"+Guid.NewGuid().ToString("N")+".tmp", temp=At(root,relative);
            try { File.Copy(source,temp,false); if(File.Exists(target)) File.Replace(temp,target,null); else File.Move(temp,target); }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        static void DeleteTree(string directory,string enclosing)
        {
            string full=Path.GetFullPath(directory), parent=Path.GetFullPath(enclosing).TrimEnd('\\')+"\\";
            if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)) throw new IOException("拒绝清理范围外的路径。");
            if(!Directory.Exists(full)) return; NoLinks(full);
            foreach(string d in Directory.GetDirectories(full)) DeleteTree(d,enclosing);
            foreach(string f in Directory.GetFiles(full)) { NoLinks(f); File.Delete(f); } Directory.Delete(full);
        }
        static void Cleanup(string path,string enclosing,Action<string> log) { try { DeleteTree(path,enclosing); } catch(Exception e) { log("临时数据尚未清理："+path+"（"+e.Message+"）"); } }
        static void VerifyChanges(Journal j,string root,string pending)
        {
            if(j==null||j.Schema!=1||!Equal(j.Root,root)||j.Changes==null||j.Changes.Count>4097) throw new InvalidOperationException("事务记录无效。");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var c in j.Changes)
            {
                if(!Equal(c.Path,StateDirectoryName+"\\manifest.json")) ManagedPath(c.Path); string target=At(root,c.Path);
                if(!seen.Add(Relative(c.Path))||c.BeforeHash!=null&&!IsHash(c.BeforeHash)||c.AfterHash!=null&&!IsHash(c.AfterHash)) throw new InvalidOperationException("事务记录中的校验数据无效。");
                if(c.BeforeHash!=null) { if(!Regex.IsMatch(c.Backup??"","^[0-9]+\\.bak$")) throw new InvalidOperationException("事务备份路径无效。"); string backup=At(pending,"backups\\"+c.Backup); if(!File.Exists(backup)||!Equal(Hash(backup),c.BeforeHash)) throw new InvalidOperationException("事务备份损坏，已保留现场："+c.Path); }
                string current=ExistingHash(target); if(!Equal(current,c.BeforeHash)&&!Equal(current,c.AfterHash)) throw new InvalidOperationException("未完成事务中的文件发生了额外修改，不能自动恢复："+c.Path);
            }
        }
        static void Restore(Journal journal,string root,string state,Action<string> log)
        {
            string pending=At(state,"pending"); VerifyChanges(journal,root,pending);
            foreach(var c in journal.Changes.AsEnumerable().Reverse())
            {
                string target=At(root,c.Path); if(Equal(ExistingHash(target),c.BeforeHash)) continue;
                if(c.BeforeHash==null) { if(File.Exists(target)) File.Delete(target); }
                else AtomicCopy(At(pending,"backups\\"+c.Backup),target,root);
                if(!Equal(ExistingHash(target),c.BeforeHash)) throw new IOException("恢复校验失败："+c.Path);
            }
            CompletePending(state,log); log("已恢复到操作前的文件状态。");
        }
        static void CompletePending(string state,Action<string> log) { string done=At(state,"completed-"+Guid.NewGuid().ToString("N")); Directory.Move(At(state,"pending"),done); Cleanup(done,state,log); }
        static void Transaction(string root,string state,List<Change> changes,Action<string> log,CancellationToken token)
        {
            string pending=At(state,"pending"); Directory.CreateDirectory(pending); Directory.CreateDirectory(At(pending,"backups")); var journal=new Journal { Root=root,Changes=changes };
            try
            {
                for(int i=0;i<changes.Count;i++)
                {
                    token.ThrowIfCancellationRequested(); var c=changes[i]; string target=At(root,c.Path); c.BeforeHash=ExistingHash(target);
                    if(!Equal(c.BeforeHash,c.ExpectedBeforeHash)) throw new IOException("文件在安装准备期间发生变化，已停止操作："+c.Path);
                    c.AfterHash=c.Source==null?null:Hash(c.Source);
                    if(c.BeforeHash!=null) { c.Backup=i+".bak"; string p=At(pending,"backups\\"+c.Backup); File.Copy(target,p,false); if(!Equal(Hash(p),c.BeforeHash)) throw new IOException("事务备份校验失败："+c.Path); }
                }
                string tmp=At(pending,"journal.tmp"); SaveJson(tmp,journal); File.Move(tmp,At(pending,"journal.json")); token.ThrowIfCancellationRequested();
                // Commit runs to completion without cancellation, or restores all touched files.
                foreach(var c in changes)
                {
                    string target=At(root,c.Path); if(!Equal(ExistingHash(target),c.BeforeHash)) throw new IOException("文件在安装准备期间发生变化："+c.Path);
                    if(c.Source==null) { if(File.Exists(target)) File.Delete(target); }
                    else AtomicCopy(c.Source,target,root);
                    if(!Equal(ExistingHash(target),c.AfterHash)) throw new IOException("写入后校验失败："+c.Path); log("已处理："+c.Path);
                }
                CompletePending(state,log);
            }
            catch(Exception error)
            {
                try { if(File.Exists(At(pending,"journal.json"))) Restore(journal,root,state,log); else Cleanup(pending,state,log); }
                catch(Exception recovery) { throw new IOException("操作失败，自动恢复未完成。请保留安装记录并点击恢复。原错误："+error.Message+"；恢复错误："+recovery.Message,error); }
                throw;
            }
        }
        public static void Recover(string gameExe,Action<string> log)
        {
            string root=Root(gameExe); using(new HeldMutex(root))
            {
                GameStopped(gameExe); string state=State(root,false),pending=At(state,"pending"),path=At(pending,"journal.json");
                if(!Directory.Exists(pending)) { log("没有需要恢复的事务。"); return; }
                if(!File.Exists(path)) { DeleteTree(pending,state); log("已清理中断的准备阶段；没有已提交的文件写入。"); return; }
                Journal journal; try { journal=Json().Deserialize<Journal>(File.ReadAllText(path)); } catch(Exception e) { throw new InvalidOperationException("事务记录损坏，请保留备份。",e); }
                Restore(journal,root,state,log);
            }
        }
        public static void Install(string packageZip,InstallOptions options,string version,Action<string> log,CancellationToken token)
        {
            if(options==null) throw new ArgumentNullException("options"); token.ThrowIfCancellationRequested(); string root=Root(options.GameExe);
            using(new HeldMutex(root))
            {
                GameStopped(options.GameExe);
                if(!Proxies.Any(p=>Equal(p,options.ProxyName))) throw new InvalidOperationException("不支持的代理 DLL 名称。");
                if(options.Gpu!="NVIDIA"&&options.Gpu!="AMD"&&options.Gpu!="Intel") throw new InvalidOperationException("请选择正确的显卡类型。");
                string state=State(root,false); NoPending(state); var old=Load(root,state);
                if(old!=null&&!Equal(old.ProxyName,options.ProxyName)) throw new InvalidOperationException("更换代理名称前，请先卸载此安装器管理的旧版本。");
                CheckCurrent(old,root); CheckOriginals(old,state);
                if(old==null)
                {
                    if(File.Exists(At(root,options.ProxyName))) throw new InvalidOperationException("代理文件已存在，可能属于 ReShade 或其他 Mod。请换一个代理名称或先处理现有安装："+options.ProxyName);
                    foreach(string name in Proxies.Concat(new[]{"OptiScaler.dll"}))
                    {
                        string p=At(root,name); if(File.Exists(p)&&(Equal(name,"OptiScaler.dll")||Equal(name,"OptiScaler.asi")||Equal(FileVersionInfo.GetVersionInfo(p).OriginalFilename,"OptiScaler.dll"))) throw new InvalidOperationException("检测到非本安装器管理的 OptiScaler，请先用原安装方式卸载："+name);
                    }
                }
                string model=String.IsNullOrWhiteSpace(options.ModelDll)?At(root,"nvngx_dlssnr.dll"):Path.GetFullPath(options.ModelDll);
                bool copyModel=!String.IsNullOrWhiteSpace(options.ModelDll)&&!Equal(model,At(root,"nvngx_dlssnr.dll"));
                if(options.EnableNeuralRendering||copyModel)
                {
                    if(!File.Exists(model)||!Equal(Path.GetFileName(model),"nvngx_dlssnr.dll")) throw new InvalidOperationException("请提供 nvngx_dlssnr.dll 模型文件，或取消默认启用 Neural Rendering。");
                    NoLinks(model); ValidatePe(model,true);
                }
                var optionalSources=GetFsrSources(options,token);
                foreach(var source in GetDlssgSources(options,token)) optionalSources.Add(source.Key,source.Value);
                string stage=Path.Combine(Path.GetTempPath(),"OptiScalerStage-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
                try
                {
                    log("检查并解压完整发布包……"); var files=Extract(packageZip,stage,token);
                    FillBridgeDll(files,stage,options,log);
                    CheckCurrent(old,root); CheckOriginals(old,state);
                    // These optional destinations are managed only through an explicit selection.
                    foreach(string path in OptionalPaths) files.Remove(path);
                    foreach(var source in optionalSources)
                    {
                        token.ThrowIfCancellationRequested(); NoLinks(source.Value);
                        string extension=Path.GetExtension(source.Key), staged=At(stage,"optional-"+Guid.NewGuid().ToString("N")+extension);
                        File.Copy(source.Value,staged,false);
                        if(Equal(extension,".dll")) ValidatePe(staged,true);
                        files.Add(source.Key,staged);
                    }
                    if(files.ContainsKey(options.ProxyName)) throw new InvalidOperationException("安装包同时包含源 DLL 和目标代理，无法确定正确文件。");
                    files.Add(options.ProxyName,files["OptiScaler.dll"]); files.Remove("OptiScaler.dll");
                    string originalIniHash=ExistingHash(At(root,"OptiScaler.ini"));
                    string ini=File.Exists(At(root,"OptiScaler.ini"))?File.ReadAllText(At(root,"OptiScaler.ini")):File.ReadAllText(files["OptiScaler.ini"]);
                    ini=SetIni(ini,"DlssNr","Enabled",options.EnableNeuralRendering?"true":"false");
                    if(options.Gpu!="NVIDIA") ini=SetIni(ini,"Spoofing","Dxgi",options.UseDlssInputs?"auto":"false");
                    File.WriteAllText(files["OptiScaler.ini"],ini,new UTF8Encoding(false));
                    if(copyModel) { string staged=At(stage,"nvngx_dlssnr.dll"); File.Copy(model,staged,false); files.Add("nvngx_dlssnr.dll",staged); }
                    token.ThrowIfCancellationRequested(); state=State(root,true); Directory.CreateDirectory(At(state,"originals"));
                    var next=new InstallManifest { Root=root,GameExe=Path.GetFileName(options.GameExe),Version=version,ProxyName=options.ProxyName,InstalledUtc=DateTime.UtcNow.ToString("o"),CreatedDirectories=old==null?new List<string>():new List<string>(old.CreatedDirectories) }; var changes=new List<Change>();
                    foreach(var item in files.OrderBy(x=>x.Key,StringComparer.OrdinalIgnoreCase))
                    {
                        token.ThrowIfCancellationRequested(); string target=At(root,item.Key); var prior=old==null?null:old.Files.Find(previous=>Equal(Relative(previous.Path),item.Key)); var f=new ManagedFile { Path=item.Key,InstalledHash=Hash(item.Value) };
                        string expected=ExistingHash(target);
                        if(Equal(item.Key,"OptiScaler.ini")&&!Equal(expected,originalIniHash)||prior!=null&&!Equal(item.Key,"OptiScaler.ini")&&expected!=null&&!Equal(expected,prior.InstalledHash)) throw new IOException("文件在准备期间发生变化，已保留："+item.Key);
                        if(prior!=null) { f.HadOriginal=prior.HadOriginal; f.OriginalHash=prior.OriginalHash; f.BackupName=prior.BackupName; }
                        else if(expected!=null) { f.HadOriginal=true; f.OriginalHash=expected; f.BackupName=Guid.NewGuid().ToString("N")+".bak"; string backup=At(state,"originals\\"+f.BackupName); File.Copy(target,backup,false); if(!Equal(Hash(backup),f.OriginalHash)) throw new IOException("原文件备份校验失败："+item.Key); }
                        next.Files.Add(f); EnsureParents(root,target,next.CreatedDirectories); changes.Add(new Change { Path=item.Key,Source=item.Value,ExpectedBeforeHash=expected });
                    }
                    if(old!=null) foreach(var f in old.Files.Where(f=>!files.ContainsKey(Relative(f.Path))))
                    {
                        if(Equal(f.Path,"nvngx_dlssnr.dll")||OptionalPaths.Any(path=>Equal(path,Relative(f.Path)))) { next.Files.Add(f); continue; }
                        changes.Add(new Change { Path=f.Path,Source=f.HadOriginal?At(state,"originals\\"+f.BackupName):null,ExpectedBeforeHash=File.Exists(At(root,f.Path))?f.InstalledHash:null });
                    }
                    string manifest=At(stage,"new-manifest.json"); SaveJson(manifest,next); changes.Add(new Change { Path=StateDirectoryName+"\\manifest.json",Source=manifest,ExpectedBeforeHash=ExistingHash(At(state,"manifest.json")) }); Transaction(root,state,changes,log,token); log("安装完成："+version+"。备份位置："+state);
                }
                finally { Cleanup(stage,Path.GetTempPath(),log); }
            }
        }
        public static void Uninstall(string gameExe,Action<string> log,CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); string root=Root(gameExe);
            using(new HeldMutex(root))
            {
                GameStopped(gameExe); string state=State(root,false); NoPending(state); var m=Load(root,state);
                if(m==null) throw new InvalidOperationException("此目录没有本安装器创建的安装记录，不能自动卸载。");
                CheckCurrent(m,root); CheckOriginals(m,state); var config=m.Files.Find(f=>Equal(f.Path,"OptiScaler.ini")); string ini=At(root,"OptiScaler.ini");
                if(config!=null&&File.Exists(ini)&&!Equal(Hash(ini),config.InstalledHash))
                {
                    string settings=At(state,"saved-settings\\OptiScaler-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".ini"); Directory.CreateDirectory(Path.GetDirectoryName(settings)); File.Copy(ini,settings,false); log("已另存你修改过的配置："+settings);
                }
                var changes=new List<Change>(); foreach(var f in m.Files) changes.Add(new Change { Path=f.Path,Source=f.HadOriginal?At(state,"originals\\"+f.BackupName):null,ExpectedBeforeHash=Equal(f.Path,"OptiScaler.ini")?ExistingHash(At(root,f.Path)):(File.Exists(At(root,f.Path))?f.InstalledHash:null) }); changes.Add(new Change { Path=StateDirectoryName+"\\manifest.json",Source=null,ExpectedBeforeHash=ExistingHash(At(state,"manifest.json")) }); Transaction(root,state,changes,log,token);
                foreach(string dir in m.CreatedDirectories.OrderByDescending(x=>x.Length)) { string p=At(root,dir); if(Directory.Exists(p)&&!Directory.EnumerateFileSystemEntries(p).Any()) Directory.Delete(p); }
                log("已卸载并还原原有文件。原始备份与另存配置保留在："+state);
            }
        }
    }
}
