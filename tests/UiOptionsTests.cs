using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using OptiScalerInstaller;

internal static class UiOptionsTestRunner
{
    static int passed;
    static T Field<T>(MainForm form, string name) where T : class
    {
        return (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    }
    static InstallOptions Read(MainForm form)
    {
        try { return (InstallOptions)typeof(MainForm).GetMethod("ReadOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS: " + name); }
    static void SelectPresr(MainForm form)
    {
        Field<ComboBox>(form, "packageSource").SelectedItem = PackageService.SourcePresrMultipass;
        typeof(MainForm).GetMethod("UpdateOptionalComponentsVisibility", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
    }
    static void SelectDagherbou(MainForm form)
    {
        Field<ComboBox>(form, "packageSource").SelectedItem = PackageService.SourceDagherbou;
        typeof(MainForm).GetMethod("UpdateOptionalComponentsVisibility", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
    }
    static MainForm Form()
    {
        var form = new MainForm();
        Field<TextBox>(form, "gameExe").Text = Assembly.GetExecutingAssembly().Location;
        return form;
    }
    [STAThread]
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "OptiScalerUiTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Run("upstream defaults to empty", delegate {
                using (var form = Form())
                {
                    var source = Field<ComboBox>(form, "packageSource");
                    Assert(source.SelectedIndex < 0, "upstream should default empty");
                    Assert(source.Items.Count >= 2, "upstream sources missing");
                }
            });
            Run("optional components default off and ignore hidden invalid paths", delegate {
                using (var form = Form())
                {
                    Field<TextBox>(form, "fsrDirectory").Text = "<invalid>";
                    Field<TextBox>(form, "streamlineDirectory").Text = "<invalid>";
                    var options = Read(form);
                    Assert(!options.InstallFsrEnabler && !options.InstallFsrNukem && !options.InstallDlssg, "default selected component");
                    Assert(options.FsrDirectory == null && options.StreamlineDirectory == null, "unused path retained");
                }
            });
            Run("FSR always enabled; Streamline always; no marketing copy", delegate {
                using (var form = Form())
                {
                    var fsrEnabler = Field<CheckBox>(form, "fsrEnabler");
                    var dlssg = Field<CheckBox>(form, "dlssg");
                    var hint = Field<Label>(form, "optionalGateHint");
                    Assert(fsrEnabler.Enabled && dlssg.Enabled, "FSR/Streamline disabled by default");
                    Assert(!hint.Text.Contains("星级") && !hint.Text.Contains("仅"), "gated/marketing copy in hint: " + hint.Text);
                    SelectDagherbou(form);
                    Assert(fsrEnabler.Enabled && dlssg.Enabled, "disabled on Dagherbou");
                    SelectPresr(form);
                    Assert(fsrEnabler.Enabled && dlssg.Enabled, "disabled on PreSR");
                }
            });
            Run("FSR and Streamline work without selecting upstream", delegate {
                string sl = Path.Combine(root, "sl-always"); Directory.CreateDirectory(sl);
                foreach (string name in InstallerEngine.GetDlssgRequiredFileNames()) File.WriteAllText(Path.Combine(sl, name), "UI fixture");
                string fsr = Path.Combine(root, "fsr-always"); Directory.CreateDirectory(fsr);
                File.WriteAllText(Path.Combine(fsr, "dlss-enabler-headless.dll"), "UI fixture");
                File.WriteAllText(Path.Combine(fsr, "dlssg_to_fsr3_amd_is_better.dll"), "UI fixture");
                using (var form = Form())
                {
                    Field<TextBox>(form, "streamlineDirectory").Text = sl;
                    Field<CheckBox>(form, "dlssg").Checked = true;
                    Field<TextBox>(form, "fsrDirectory").Text = fsr;
                    Field<CheckBox>(form, "fsrEnabler").Checked = true;
                    Field<CheckBox>(form, "fsrNukem").Checked = true;
                    var options = Read(form);
                    Assert(options.InstallDlssg && options.InstallFsrEnabler && options.InstallFsrNukem, "FSR/Streamline blocked without upstream");
                }
            });
            Run("selected DLSSG rejects missing required files unless SDK or local present", delegate {
                string folder = Path.Combine(root, "dlssg"); Directory.CreateDirectory(folder);
                using (var form = Form())
                {
                    Field<TextBox>(form, "streamlineDirectory").Text = folder;
                    Field<CheckBox>(form, "dlssg").Checked = true;
                    bool rejected = false;
                    try { Read(form); }
                    catch (InvalidOperationException ex) { rejected = ex.Message.Contains("刷新 SDK") || ex.Message.Contains("Streamline"); }
                    Assert(rejected, "empty streamline source accepted without SDK");
                    string[] files = InstallerEngine.GetDlssgRequiredFileNames();
                    foreach (string name in files) File.WriteAllText(Path.Combine(folder, name), "UI existence fixture");
                    foreach (string name in files)
                    {
                        string file = Path.Combine(folder, name); File.Delete(file);
                        rejected = false;
                        try { Read(form); }
                        catch (InvalidOperationException ex) { rejected = ex.Message.Contains("刷新 SDK") || ex.Message.Contains("Streamline") || ex.Message.Contains(name); }
                        Assert(rejected, "missing file accepted: " + name);
                        File.WriteAllText(file, "UI existence fixture");
                    }
                }
            });
            Run("FSR and DLSSG selections and source directories stay independent", delegate {
                string fsr = Path.Combine(root, "FSR sources"), sl = Path.Combine(root, "dlssg");
                Directory.CreateDirectory(fsr);
                File.WriteAllText(Path.Combine(fsr, "dlss-enabler-headless.dll"), "UI fixture");
                File.WriteAllText(Path.Combine(fsr, "dlssg_to_fsr3_amd_is_better.dll"), "UI fixture");
                Directory.CreateDirectory(sl);
                foreach (string name in InstallerEngine.GetDlssgRequiredFileNames()) File.WriteAllText(Path.Combine(sl, name), "UI fixture");
                using (var form = Form())
                {
                    Field<TextBox>(form, "fsrDirectory").Text = fsr;
                    Field<TextBox>(form, "streamlineDirectory").Text = sl;
                    Field<CheckBox>(form, "fsrEnabler").Checked = true;
                    Field<CheckBox>(form, "fsrNukem").Checked = true;
                    Field<CheckBox>(form, "dlssg").Checked = true;
                    var all = Read(form);
                    Assert(all.InstallFsrEnabler && all.InstallFsrNukem && all.InstallDlssg, "selection lost");
                    Assert(all.FsrDirectory == fsr && all.StreamlineDirectory == sl, "source mapping incorrect");
                    Field<CheckBox>(form, "dlssg").Checked = false;
                    Field<TextBox>(form, "streamlineDirectory").Text = "<invalid>";
                    var fsrOnly = Read(form);
                    Assert(!fsrOnly.InstallDlssg && fsrOnly.StreamlineDirectory == null && fsrOnly.InstallFsrEnabler && fsrOnly.InstallFsrNukem, "uncheck affected other components");
                }
            });
            Run("busy state disables optional component inputs", delegate {
                using (var form = Form())
                {
                    typeof(MainForm).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true);
                    typeof(MainForm).GetMethod("UpdateEnabledState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                    foreach (string name in new[] { "fsrEnabler", "fsrNukem", "dlssg", "fsrDirectory", "streamlineDirectory" })
                        Assert(!Field<Control>(form, name).Enabled, "input enabled while busy: " + name);
                }
            });
            Run("check versions enabled after selecting upstream", delegate {
                using (var form = Form())
                {
                    var check = Field<Button>(form, "checkVersions");
                    var releases = Field<ComboBox>(form, "releases");
                    Assert(!check.Enabled, "check enabled before selecting source");
                    Assert(!releases.Enabled, "releases enabled before selecting source");
                    SelectPresr(form);
                    Assert(check.Enabled, "check still disabled after PreSR selected");
                    Assert(releases.Enabled, "releases still disabled after PreSR selected");
                }
            });
            Console.WriteLine(passed + " UI option tests passed; no visible windows or games opened.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            string full = Path.GetFullPath(root), expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\') + "\\";
            if (full.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("OptiScalerUiTests-", StringComparison.Ordinal))
            { try { Directory.Delete(full, true); } catch { } }
        }
    }
}
