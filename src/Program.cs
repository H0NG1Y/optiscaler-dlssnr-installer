using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OptiScalerInstaller
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            string smokePath = null;
            if (args.Length != 0)
            {
                if (args.Length != 2 || args[0] != "--ui-smoke")
                {
                    MessageBox.Show("参数用法：--ui-smoke <截图 PNG 路径>", "OptiScaler 安装助手",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 1;
                }
                smokePath = args[1];
            }

            try { SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(smokePath));
            return Environment.ExitCode;
        }
    }
}
