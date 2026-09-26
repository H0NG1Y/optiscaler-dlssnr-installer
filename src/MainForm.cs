using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OptiScalerInstaller
{
    public sealed class MainForm : Form
    {
        private readonly ModernTextBox gameExe = new ModernTextBox();
        private readonly ModernTextBox localZip = new ModernTextBox();
        private readonly ModernTextBox modelDll = new ModernTextBox();
        private readonly ModernTextBox fsrDirectory = new ModernTextBox();
        private readonly ModernTextBox streamlineDirectory = new ModernTextBox();
        private readonly ModernComboBox releases = new ModernComboBox();
        private readonly ModernComboBox packageSource = new ModernComboBox();
        private readonly ModernComboBox proxy = new ModernComboBox();
        private readonly ModernComboBox gpu = new ModernComboBox();
        private readonly CheckBox neuralRendering = new CheckBox();
        private readonly CheckBox useDlssInputs = new CheckBox();
        private readonly CheckBox fsrEnabler = new CheckBox();
        private readonly CheckBox fsrNukem = new CheckBox();
        private readonly CheckBox dlssg = new CheckBox();
        private readonly ModernComboBox streamlineSdkReleases = new ModernComboBox();
        private readonly Button checkStreamlineSdk = MakeButton("刷新 SDK");
        private readonly Button checkVersions = MakeButton("检查版本");
        private readonly Button install = MakeButton("安装 / 更新");
        private readonly Button uninstall = MakeButton("卸载并还原");
        private readonly Button recover = MakeButton("恢复中断操作");
        private readonly Button cancel = MakeButton("取消当前操作");
        private readonly Button official = MakeButton("打开官方页面");
        private readonly Label status = MakeLabel("请选择游戏实际运行的 EXE。", 9F, false);
        private readonly Label progressText = MakeLabel("就绪", 9F, false);
        private readonly ModernProgressBar progress = new ModernProgressBar();
        private readonly ModernLogBox logBox = new ModernLogBox();
        private readonly Label logPreview = MakeLabel("", 8.5F, false);
        private readonly List<Control> inputControls = new List<Control>();
        private TableLayoutPanel fsrChoices;
        private TableLayoutPanel fsrDetails;
        private TableLayoutPanel streamlineRow;
        private TableLayoutPanel streamlineDetails;
        private Label optionalGateHint;
        private CancellationTokenSource operationCancellation;
        private bool busy;
        private bool operationCancellable;
        private bool hasPending;
        private InstallManifest currentManifest;
        private readonly string smokePath;
        private Icon appIcon;

        public MainForm() : this(null) { }

        public MainForm(string smokePath)
        {
            this.smokePath = smokePath;
            Text = "OptiScaler DLSS NR 安装助手";
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); if (appIcon != null) Icon = appIcon; }
            catch (Exception) { }
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = ModernTheme.Background;
            ForeColor = ModernTheme.Ink;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1100, 880);
            MinimumSize = new Size(980, 720);
            DoubleBuffered = true;

            TableLayoutPanel layout = NewGrid(1);
            layout.RowCount = 0;
            layout.Padding = new Padding(24, 16, 24, 14);

            Label intro = MakeLabel("为游戏配置 Neural Rendering · 发布包 · 原文件备份与还原", 9F, false);
            intro.Margin = new Padding(0, 0, 0, 10);
            AddRow(layout, intro, false);

            TableLayoutPanel gameContent;
            ModernCard gameCard = CreateSection("1", "选择游戏", out gameContent);
            AddPathRow(gameContent, "游戏 EXE", gameExe, "浏览…", BrowseGame, null);
            SetPlaceholder(gameExe, "选择游戏实际运行的 EXE，非启动器");
            status.ForeColor = ModernTheme.Muted;
            status.Margin = new Padding(0, 6, 0, 0);
            status.Font = new Font("Microsoft YaHei UI", 8.5F);
            AddRow(gameContent, status, false);
            AddRow(layout, gameCard, false);

            TableLayoutPanel middle = NewGrid(2);
            middle.ColumnStyles.Clear();
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            middle.Margin = new Padding(0, 0, 0, 12);
            TableLayoutPanel sourceContent;
            ModernCard sourceCard = CreateSection("2", "安装来源与模型", out sourceContent);
            sourceCard.Margin = new Padding(0, 0, 12, 0);
            packageSource.DropDownStyle = ComboBoxStyle.DropDownList;
            packageSource.Placeholder = "请选择上游仓库";
            foreach (UpstreamSource source in PackageService.SupportedSources) packageSource.Items.Add(source);
            packageSource.SelectedIndex = -1;
            AddFieldRow(sourceContent, "上游仓库", packageSource, null);
            inputControls.Add(packageSource);
            inputControls.Add(gameExe);
            inputControls.Add(localZip);
            inputControls.Add(modelDll);
            inputControls.Add(fsrDirectory);
            inputControls.Add(streamlineDirectory);
            packageSource.SelectedIndexChanged += delegate
            {
                releases.Items.Clear();
                UpdateOptionalComponentsVisibility();
                UpdateEnabledState();
            };
            localZip.TextChanged += delegate { UpdateEnabledState(); };
            releases.DropDownWidth = 740;
            releases.Placeholder = "先选上游，再检查版本";
            Button checkVersionsInRow = checkVersions;
            checkVersions.Margin = Padding.Empty;
            AddFieldRow(sourceContent, "官方版本", releases, ActionStrip(checkVersionsInRow));
            inputControls.Add(releases);
            inputControls.Add(checkVersions);
            AddPathRow(sourceContent, "本地 ZIP", localZip, "浏览…", BrowseZip, delegate { localZip.Clear(); });
            AddPathRow(sourceContent, "NR 模型", modelDll, "浏览…", BrowseModel, delegate { modelDll.Clear(); });
            SetPlaceholder(localZip, "可选 · 从所选上游页面下载的 ZIP");
            SetPlaceholder(modelDll, "nvngx_dlssnr.dll，留空复用已有模型");
            Label packageHint = MakeLabel("请先选择上游仓库，再检查版本。本地 ZIP 优先；模型与驱动请按所选版本匹配。", 8.5F, false);
            packageHint.ForeColor = ModernTheme.Muted;
            packageHint.Margin = new Padding(0, 8, 0, 0);
            AddRow(sourceContent, packageHint, false);
            middle.Controls.Add(sourceCard, 0, 0);

            TableLayoutPanel optionsContent;
            ModernCard optionsCard = CreateSection("3", "安装选项", out optionsContent);
            optionsCard.Margin = Padding.Empty;
            proxy.Items.AddRange(new object[] { "dxgi.dll", "winmm.dll", "version.dll", "dbghelp.dll", "d3d12.dll", "wininet.dll", "winhttp.dll", "OptiScaler.asi" });
            proxy.SelectedIndex = 0;
            gpu.Items.AddRange(new object[] { "NVIDIA", "AMD", "Intel" });
            gpu.SelectedIndex = 0;
            AddFieldRow(optionsContent, "代理文件", proxy, null);
            AddFieldRow(optionsContent, "显卡", gpu, null);
            neuralRendering.Text = "启用 Neural Rendering";
            neuralRendering.AutoSize = true;
            neuralRendering.FlatStyle = FlatStyle.Flat;
            neuralRendering.Checked = true;
            neuralRendering.Margin = new Padding(0, 8, 0, 4);
            AddRow(optionsContent, neuralRendering, false);
            useDlssInputs.Text = "使用 DLSS 输入（AMD / Intel）";
            useDlssInputs.AutoSize = true;
            useDlssInputs.Checked = true;
            useDlssInputs.Enabled = false;
            useDlssInputs.FlatStyle = FlatStyle.Flat;
            useDlssInputs.Margin = new Padding(0, 3, 0, 5);
            AddRow(optionsContent, useDlssInputs, false);
            Label compatibility = MakeLabel("默认 dxgi.dll。选 ASI 代理时，游戏需已有 ASI loader。", 8.5F, false);
            compatibility.ForeColor = ModernTheme.Muted;
            compatibility.Margin = new Padding(0, 7, 0, 0);
            AddRow(optionsContent, compatibility, false);
            inputControls.Add(proxy);
            inputControls.Add(gpu);
            inputControls.Add(neuralRendering);
            inputControls.Add(useDlssInputs);
            middle.Controls.Add(optionsCard, 1, 0);
            AddRow(layout, middle, false);

            TableLayoutPanel fsrContent;
            ModernCard fsrCard = CreateSection("4", "附加与兼容修复", out fsrContent);
            optionalGateHint = MakeLabel("Streamline SDK：启用 OptiScaler 帧生成，写入 OptiScaler\\streamline。FSR：本地目录提供 DLL。", 8.5F, false);
            optionalGateHint.ForeColor = ModernTheme.Muted;
            optionalGateHint.Margin = new Padding(0, 4, 0, 0);
            AddRow(fsrContent, optionalGateHint, false);

            streamlineRow = NewGrid(1);
            streamlineRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            streamlineRow.Margin = new Padding(0, 2, 0, 0);
            dlssg.Text = "Streamline 帧生成 SDK";
            dlssg.AutoSize = true;
            dlssg.FlatStyle = FlatStyle.Flat;
            dlssg.Margin = new Padding(0, 3, 8, 3);
            streamlineRow.Controls.Add(dlssg, 0, 0);
            AddRow(fsrContent, streamlineRow, false);
            inputControls.Add(dlssg);

            fsrChoices = NewGrid(2);
            fsrChoices.ColumnStyles.Clear();
            fsrChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fsrChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fsrChoices.Margin = new Padding(0, 2, 0, 0);
            fsrEnabler.Text = "FSR 3 多帧生成 · Enabler";
            fsrNukem.Text = "FSR 3 帧生成 · Nukem's";
            foreach (CheckBox choice in new[] { fsrEnabler, fsrNukem })
            {
                choice.AutoSize = true;
                choice.FlatStyle = FlatStyle.Flat;
                choice.Margin = new Padding(0, 3, 8, 3);
                inputControls.Add(choice);
            }
            fsrChoices.Controls.Add(fsrEnabler, 0, 0);
            fsrChoices.Controls.Add(fsrNukem, 1, 0);
            AddRow(fsrContent, fsrChoices, false);

            fsrDetails = NewGrid(1);
            fsrDetails.RowCount = 0;
            fsrDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fsrDetails.Margin = new Padding(0, 5, 0, 0);
            AddPathRow(fsrDetails, "组件目录", fsrDirectory, "浏览…", BrowseFsrDirectory, delegate { fsrDirectory.Clear(); });
            SetPlaceholder(fsrDirectory, "选择存放 FSR 帧生成 DLL 的文件夹");
            Label fsrHint = MakeLabel("DLL 须本地提供。可同时安装，游戏菜单中选用一种；不会自动开启帧生成。", 8.5F, false);
            fsrHint.ForeColor = ModernTheme.Muted;
            fsrHint.Margin = new Padding(0, 5, 0, 0);
            AddRow(fsrDetails, fsrHint, false);
            AddRow(fsrContent, fsrDetails, false);
            EventHandler updateFsrDetails = delegate
            {
                fsrDetails.Visible = fsrEnabler.Checked || fsrNukem.Checked;
                Relayout();
            };
            fsrEnabler.CheckedChanged += updateFsrDetails;
            fsrNukem.CheckedChanged += updateFsrDetails;

            streamlineDetails = NewGrid(1);
            streamlineDetails.RowCount = 0;
            streamlineDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            streamlineDetails.Margin = new Padding(0, 5, 0, 0);
            streamlineSdkReleases.Placeholder = "NVIDIA-RTX Streamline SDK";
            streamlineSdkReleases.DropDownWidth = 720;
            checkStreamlineSdk.Margin = Padding.Empty;
            AddFieldRow(streamlineDetails, "SDK 版本", streamlineSdkReleases, ActionStrip(checkStreamlineSdk));
            inputControls.Add(streamlineSdkReleases);
            inputControls.Add(checkStreamlineSdk);
            AddPathRow(streamlineDetails, "本地目录", streamlineDirectory, "浏览…", BrowseStreamlineDirectory, delegate { streamlineDirectory.Clear(); });
            SetPlaceholder(streamlineDirectory, "可选 · 已有 SDK 运行库目录；留空则用上方 SDK 下载");
            Label streamlineHint = MakeLabel("用于启用 OptiScaler 自身帧生成：从 NVIDIA Streamline SDK 的 bin/x64 提取 6 个 DLL，写入游戏目录 OptiScaler\\streamline\\，不覆盖游戏 Engine 自带的 Streamline。", 8.5F, false);
            streamlineHint.ForeColor = ModernTheme.Muted;
            streamlineHint.Margin = new Padding(0, 5, 0, 0);
            AddRow(streamlineDetails, streamlineHint, false);
            AddRow(fsrContent, streamlineDetails, false);
            dlssg.CheckedChanged += delegate
            {
                streamlineDetails.Visible = dlssg.Checked;
                if (dlssg.Checked && streamlineSdkReleases.Items.Count == 0 && IsHandleCreated)
                    BeginInvoke(new MethodInvoker(async delegate { await RefreshStreamlineSdkAsync(); }));
                Relayout();
            };
            checkStreamlineSdk.Click += async delegate { await RefreshStreamlineSdkAsync(); };
            UpdateOptionalComponentsVisibility();
            AddRow(layout, fsrCard, false);

            TableLayoutPanel actionArea = NewGrid(5);
            actionArea.ColumnStyles.Clear();
            actionArea.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionArea.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionArea.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionArea.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionArea.Margin = new Padding(0, 0, 0, 6);
            ((ModernButton)install).Primary = true;
            install.MinimumSize = new Size(154, 43);
            install.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            uninstall.MinimumSize = new Size(112, 41);
            recover.MinimumSize = new Size(128, 41);
            ((ModernButton)uninstall).Quiet = true;
            ((ModernButton)recover).Quiet = true;
            actionArea.Controls.Add(install, 0, 0);
            actionArea.Controls.Add(uninstall, 1, 0);
            actionArea.Controls.Add(recover, 2, 0);
            cancel.Enabled = false;
            cancel.Anchor = AnchorStyles.Right;
            cancel.Margin = new Padding(8, 2, 0, 2);
            ((ModernButton)cancel).Quiet = true;
            actionArea.Controls.Add(cancel, 4, 0);
            AddRow(layout, actionArea, false);
            inputControls.Add(install);
            inputControls.Add(uninstall);
            inputControls.Add(recover);

            TableLayoutPanel progressRow = NewGrid(2);
            progressRow.ColumnStyles.Clear();
            progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
            progressRow.Margin = new Padding(0, 0, 0, 10);
            progress.Dock = DockStyle.Fill;
            progress.Height = 8;
            progress.Margin = new Padding(0, 6, 14, 6);
            progressRow.Controls.Add(progress, 0, 0);
            progressText.ForeColor = ModernTheme.Muted;
            progressText.TextAlign = ContentAlignment.MiddleRight;
            progressText.Margin = Padding.Empty;
            progressRow.Controls.Add(progressText, 1, 0);
            AddRow(layout, progressRow, false);

            TableLayoutPanel logArea = NewGrid(1);
            logArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            logPreview.Dock = DockStyle.Top;
            logPreview.Margin = new Padding(0, 0, 0, 4);
            logPreview.Height = 20;
            AddRow(logArea, logPreview, false);
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.BorderStyle = BorderStyle.None;
            logBox.BackColor = Color.White;
            logBox.ForeColor = ModernTheme.Ink;
            logBox.Font = new Font("Microsoft YaHei UI", 8.5F);
            logBox.Height = 72;
            AddRow(logArea, logBox, false);
            AddRow(layout, logArea, true);

            TableLayoutPanel footer = NewGrid(2);
            footer.ColumnStyles.Clear();
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Label footNote = MakeLabel("Insert 打开 OptiScaler 菜单。安装器不修改注册表，不自动启动游戏。", 8.5F, false);
            footNote.ForeColor = ModernTheme.Muted;
            footer.Controls.Add(footNote, 0, 0);
            official.Margin = new Padding(8, 0, 0, 0);
            ((ModernButton)official).Quiet = true;
            footer.Controls.Add(official, 1, 0);
            AddRow(layout, footer, false);

            Controls.Add(layout);

            gameExe.TextChanged += delegate { status.Text = DescribeGameSelection(); RefreshInstallState(); };
            gpu.SelectedIndexChanged += delegate { useDlssInputs.Enabled = (string)gpu.SelectedItem != "NVIDIA"; };
            checkVersions.Click += async delegate { await CheckVersionsAsync(); };
            install.Click += async delegate { await InstallAsync(); };
            uninstall.Click += async delegate { await UninstallAsync(); };
            recover.Click += async delegate { await RecoverAsync(); };
            cancel.Click += delegate
            {
                if (operationCancellation != null && operationCancellable && !operationCancellation.IsCancellationRequested)
                {
                    cancel.Enabled = false;
                    progressText.Text = "正在取消…";
                    operationCancellation.Cancel();
                }
            };
            official.Click += delegate { OpenOfficialPage(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (busy)
                {
                    e.Cancel = true;
                    string closeMessage = operationCancellable && operationCancellation != null && !operationCancellation.IsCancellationRequested
                        ? "当前操作尚未结束。请先点击“取消当前操作”，并等待安全结束后关闭窗口。"
                        : "正在等待文件操作安全结束。请稍候，完成后即可关闭窗口。";
                    MessageBox.Show(this, closeMessage, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            Shown += delegate
            {
                FitToScreen();
                string bundledModel = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nvngx_dlssnr.dll");
                if (File.Exists(bundledModel))
                {
                    modelDll.Text = bundledModel;
                    WriteLog("已自动选择安装器目录中的 NR 模型；需要使用其他模型时可重新选择。 ");
                }
                string bundledFsr = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FSR");
                if (Directory.Exists(bundledFsr)) fsrDirectory.Text = bundledFsr;
                string bundledStreamline = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "streamline");
                if (Directory.Exists(bundledStreamline)) streamlineDirectory.Text = bundledStreamline;
                UpdateOptionalComponentsVisibility();
                if (this.smokePath != null)
                {
                    BeginInvoke(new MethodInvoker(SaveSmokeScreenshot));
                    return;
                }
            };
            WriteLog("请选择游戏真正加载图形模块的 EXE；启动器可能位于不同目录。 ");
            WriteLog("请先选择上游仓库，再检查版本。 ");
            UpdateEnabledState();
        }

        private static Label MakeLabel(string text, float size, bool bold)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            label.Margin = Padding.Empty;
            return label;
        }

        private static Button MakeButton(string text)
        {
            Button button = new ModernButton();
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(88, 38);
            button.Padding = new Padding(7, 2, 7, 2);
            button.Margin = new Padding(0, 2, 8, 3);
            return button;
        }

        private static TableLayoutPanel NewGrid(int columns)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.ColumnCount = columns;
            panel.RowCount = 0;
            panel.Dock = DockStyle.Fill;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Margin = Padding.Empty;
            panel.BackColor = Color.Transparent;
            return panel;
        }

        private static ModernCard CreateSection(string number, string title, out TableLayoutPanel content)
        {
            ModernCard card = new ModernCard();
            card.Dock = DockStyle.Top;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.Margin = new Padding(0, 0, 0, 12);
            card.Padding = new Padding(16, 14, 16, 14);
            content = NewGrid(1);
            content.Dock = DockStyle.Fill;
            Label header = MakeLabel(number + "  " + title, 10F, true);
            header.Margin = new Padding(0, 0, 0, 8);
            content.Controls.Add(header, 0, 0);
            content.RowCount = 1;
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(content);
            return card;
        }

        private const int LabelColumnWidth = 88;
        private const int ActionColumnWidth = 160;
        private const int FieldRowHeight = 36;

        private static Label FieldLabel(string text)
        {
            Label label = MakeLabel(text, 9F, false);
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Margin = Padding.Empty;
            label.Padding = new Padding(0, 0, 10, 0);
            return label;
        }

        private void AddRow(TableLayoutPanel panel, Control control, bool fill)
        {
            int row = panel.RowCount;
            if (row < 0) row = 0;
            panel.RowCount = row + 1;
            if (panel.RowStyles.Count < panel.RowCount) panel.RowStyles.Add(new RowStyle(fill ? SizeType.Percent : SizeType.AutoSize, fill ? 100 : 0));
            panel.Controls.Add(control, 0, row);
        }

        private static Control WrapInput(Control box)
        {
            ModernTextBox modern = box as ModernTextBox;
            if (modern != null)
            {
                modern.BorderStyle = BorderStyle.None;
                modern.Margin = Padding.Empty;
                ModernInputChrome chrome = new ModernInputChrome(modern);
                chrome.Dock = DockStyle.Fill;
                chrome.Margin = Padding.Empty;
                chrome.MinimumSize = new Size(80, FieldRowHeight);
                chrome.Height = FieldRowHeight;
                return chrome;
            }
            ComboBox combo = box as ComboBox;
            if (combo != null)
            {
                combo.Dock = DockStyle.Fill;
                combo.Margin = Padding.Empty;
                combo.MinimumSize = new Size(80, FieldRowHeight);
                combo.Height = FieldRowHeight;
                return combo;
            }
            box.Dock = DockStyle.Fill;
            box.Margin = Padding.Empty;
            return box;
        }

        private static Control ActionStrip(params Control[] parts)
        {
            TableLayoutPanel strip = new TableLayoutPanel();
            strip.ColumnCount = Math.Max(1, parts.Length);
            strip.RowCount = 1;
            strip.Dock = DockStyle.Fill;
            strip.AutoSize = false;
            strip.Margin = Padding.Empty;
            strip.BackColor = Color.Transparent;
            float each = 100F / strip.ColumnCount;
            for (int i = 0; i < strip.ColumnCount; i++) strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, each));
            strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < parts.Length; i++)
            {
                Control part = parts[i];
                if (part == null) continue;
                part.Dock = DockStyle.Fill;
                part.Margin = new Padding(4, 0, 0, 0);
                Button button = part as Button;
                if (button != null) { button.AutoSize = false; button.MinimumSize = Size.Empty; }
                strip.Controls.Add(part, i, 0);
            }
            strip.Height = FieldRowHeight;
            strip.MinimumSize = new Size(0, FieldRowHeight);
            return strip;
        }

        /// <summary>Shared form grid: label 88 | input fill | actions 160, row height 36.</summary>
        private void AddFieldRow(TableLayoutPanel parent, string label, Control input, Control actions)
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.ColumnCount = 3;
            row.RowCount = 1;
            row.AutoSize = false;
            row.Height = FieldRowHeight;
            row.Dock = DockStyle.Top;
            row.Margin = new Padding(0, 2, 0, 2);
            row.BackColor = Color.Transparent;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelColumnWidth));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ActionColumnWidth));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.Controls.Add(FieldLabel(label ?? ""), 0, 0);
            row.Controls.Add(WrapInput(input), 1, 0);
            if (actions != null) row.Controls.Add(actions, 2, 0);
            AddRow(parent, row, false);
        }

        private void AddPathRow(TableLayoutPanel layout, string label, TextBox box, string buttonText, EventHandler browse, EventHandler clear)
        {
            Button browseButton = MakeButton(buttonText);
            browseButton.Click += browse;
            Control actions;
            if (clear != null)
            {
                Button clearButton = MakeButton("清空");
                ((ModernButton)clearButton).Quiet = true;
                clearButton.Click += clear;
                actions = ActionStrip(browseButton, clearButton);
            }
            else actions = ActionStrip(browseButton);
            // Always use the shared label|input|action grid so path boxes line up with combos.
            AddFieldRow(layout, label, box, actions);
        }

        private static void SetPlaceholder(TextBox box, string text)
        {
            ModernTextBox modern = box as ModernTextBox;
            if (modern != null) modern.Placeholder = text;
        }

        private UpstreamSource SelectedSource()
        {
            return packageSource.SelectedItem as UpstreamSource;
        }

        private bool FsrComponentsAllowed()
        {
            return true;
        }

        private void Relayout()
        {
            if (IsDisposed) return;
            foreach (Control child in Controls) { child.PerformLayout(); child.Refresh(); }
            PerformLayout();
            Refresh();
        }

        private void UpdateOptionalComponentsVisibility()
        {
            if (fsrChoices != null) fsrChoices.Visible = true;
            if (streamlineRow != null) streamlineRow.Visible = true;
            if (fsrDetails != null) fsrDetails.Visible = fsrEnabler.Checked || fsrNukem.Checked;
            if (streamlineDetails != null) streamlineDetails.Visible = dlssg.Checked;
            if (optionalGateHint != null)
                optionalGateHint.Text = "Streamline SDK：启用 OptiScaler 帧生成，写入 OptiScaler\\streamline。FSR：本地目录提供 DLL。";
            if (fsrEnabler != null) fsrEnabler.Enabled = !busy;
            if (fsrNukem != null) fsrNukem.Enabled = !busy;
            if (dlssg != null) dlssg.Enabled = !busy;
            if (streamlineSdkReleases != null) streamlineSdkReleases.Enabled = !busy && dlssg.Checked;
            if (checkStreamlineSdk != null) checkStreamlineSdk.Enabled = !busy && dlssg.Checked;
            Relayout();
        }

        private string DescribeGameSelection()
        {
            string path = gameExe.Text.Trim().Trim('"');
            if (path.Length == 0 || !File.Exists(path)) return "请选择存在的游戏 EXE 文件。";
            try
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                string name = String.IsNullOrEmpty(info.FileDescription) ? Path.GetFileName(path) : info.FileDescription;
                return "已选择：" + name;
            }
            catch (Exception) { return "已选择：" + Path.GetFileName(path); }
        }

        private void RefreshInstallState()
        {
            try
            {
                string path = gameExe.Text.Trim().Trim('"');
                if (!File.Exists(path)) { currentManifest = null; hasPending = false; UpdateEnabledState(); return; }
                currentManifest = InstallerEngine.ReadManifest(path);
                hasPending = false;
                UpdateEnabledState();
            }
            catch (Exception ex)
            {
                currentManifest = null;
                status.Text = "无法读取安装状态：" + ex.Message;
                UpdateEnabledState();
            }
        }

        private void UpdateEnabledState()
        {
            foreach (Control control in inputControls) control.Enabled = !busy;
            if (busy) { UpdateOptionalComponentsVisibility(); return; }
            bool hasSource = SelectedSource() != null;
            bool online = localZip.Text.Trim().Length == 0;
            releases.Enabled = online && hasSource;
            packageSource.Enabled = true;
            checkVersions.Enabled = online && hasSource;
            useDlssInputs.Enabled = (string)gpu.SelectedItem != "NVIDIA";
            install.Enabled = !hasPending;
            uninstall.Enabled = currentManifest != null && !hasPending;
            recover.Enabled = hasPending;
            UpdateOptionalComponentsVisibility();
        }

        private void WriteLog(string message)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string>(WriteLog), message); } catch (ObjectDisposedException) { } return; }
            string entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message.TrimEnd();
            logPreview.Text = entry.Replace('\r', ' ').Replace('\n', ' ');
            logBox.AppendText((logBox.TextLength == 0 ? "" : Environment.NewLine) + entry);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private void ShowDownloadProgress(DownloadProgress value)
        {
            if (IsDisposed || value == null) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<DownloadProgress>(ShowDownloadProgress), value); } catch (ObjectDisposedException) { } return; }
            if (value.Total > 0)
            {
                int percent = (int)(value.Received * 100L / value.Total);
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = Math.Max(0, Math.Min(100, percent));
                progressText.Text = "下载 " + percent + "%";
            }
        }

        private async Task RunOperationAsync(string title, Func<CancellationToken, Task> body, bool cancellable)
        {
            if (busy) return;
            busy = true;
            operationCancellable = cancellable;
            operationCancellation = cancellable ? new CancellationTokenSource() : null;
            progress.Style = ProgressBarStyle.Marquee;
            progressText.Text = title;
            UpdateEnabledState();
            try { await body(operationCancellation == null ? CancellationToken.None : operationCancellation.Token); }
            catch (OperationCanceledException) { WriteLog("操作已取消。"); progressText.Text = "已取消"; }
            catch (Exception ex)
            {
                WriteLog("错误：" + ex.Message);
                progressText.Text = "操作失败";
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                operationCancellable = false;
                if (operationCancellation != null) { operationCancellation.Dispose(); operationCancellation = null; }
                cancel.Enabled = false;
                if (progress.Style == ProgressBarStyle.Marquee) progress.Style = ProgressBarStyle.Continuous;
                RefreshInstallState();
                if (progressText.Text == title) progressText.Text = "就绪";
            }
        }

        private void BrowseGame(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择游戏实际运行的 EXE";
                dialog.Filter = "游戏 EXE|*.exe";
                if (File.Exists(gameExe.Text)) dialog.InitialDirectory = Path.GetDirectoryName(gameExe.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK) gameExe.Text = dialog.FileName;
            }
        }

        private void BrowseZip(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择从上游 Releases 下载的原始 ZIP";
                dialog.Filter = "ZIP|*.zip";
                if (dialog.ShowDialog(this) == DialogResult.OK) localZip.Text = dialog.FileName;
            }
        }

        private void BrowseModel(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 nvngx_dlssnr.dll 模型";
                dialog.Filter = "模型 DLL|*.dll";
                if (File.Exists(modelDll.Text)) dialog.InitialDirectory = Path.GetDirectoryName(modelDll.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK) modelDll.Text = dialog.FileName;
            }
        }

        private void BrowseFsrDirectory(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择存放 FSR 帧生成 DLL 的文件夹";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(fsrDirectory.Text)) dialog.SelectedPath = fsrDirectory.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) fsrDirectory.Text = dialog.SelectedPath;
            }
        }

        private void BrowseStreamlineDirectory(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择包含 Streamline 与 nvngx_dlssg.dll 的文件夹";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(streamlineDirectory.Text)) dialog.SelectedPath = streamlineDirectory.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) streamlineDirectory.Text = dialog.SelectedPath;
            }
        }

        private async Task CheckVersionsAsync()
        {
            UpstreamSource source = SelectedSource();
            if (source == null)
            {
                WriteLog("请先选择上游仓库。 ");
                MessageBox.Show(this, "请先选择上游仓库，再检查版本。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string previousTag = releases.SelectedItem == null ? null : ((ReleaseInfo)releases.SelectedItem).Tag;
            await RunOperationAsync("正在查询版本…", async delegate(CancellationToken token)
            {
                List<ReleaseInfo> found = await PackageService.FetchReleasesAsync(source, token);
                releases.Items.Clear();
                foreach (ReleaseInfo release in found) releases.Items.Add(release);
                if (releases.Items.Count > 0)
                {
                    releases.SelectedIndex = 0;
                    for (int i = 0; i < releases.Items.Count; i++)
                        if (((ReleaseInfo)releases.Items[i]).Tag == previousTag) releases.SelectedIndex = i;
                    WriteLog("已读取 " + releases.Items.Count + " 个发布包。选择版本后可安装，也可使用本地 ZIP。 ");
                }
                else WriteLog("接口未返回可用 ZIP，请打开该仓库页面检查发布包。 ");
                progressText.Text = "版本检查完成";
            }, true);
        }

        private async Task RefreshStreamlineSdkAsync()
        {
            string previous = streamlineSdkReleases.SelectedItem == null ? null : ((ReleaseInfo)streamlineSdkReleases.SelectedItem).Tag;
            try
            {
                streamlineSdkReleases.Enabled = false;
                List<ReleaseInfo> found = await PackageService.FetchStreamlineSdkReleasesAsync(CancellationToken.None);
                streamlineSdkReleases.Items.Clear();
                foreach (ReleaseInfo release in found)
                    if (PackageService.IsStreamlineSdkAsset(release.AssetName))
                        streamlineSdkReleases.Items.Add(release);
                if (streamlineSdkReleases.Items.Count > 0)
                {
                    streamlineSdkReleases.SelectedIndex = 0;
                    for (int i = 0; i < streamlineSdkReleases.Items.Count; i++)
                        if (((ReleaseInfo)streamlineSdkReleases.Items[i]).Tag == previous) streamlineSdkReleases.SelectedIndex = i;
                    ReleaseInfo selected = streamlineSdkReleases.SelectedItem as ReleaseInfo;
                    WriteLog("已读取 Streamline SDK 版本 " + streamlineSdkReleases.Items.Count + " 项" + (selected == null ? "" : "（当前 " + selected.Tag + "）") + "。 ");
                }
                else WriteLog("Streamline SDK 接口未返回可用 x64 ZIP。 ");
            }
            catch (Exception ex) { WriteLog("查询 Streamline SDK 失败：" + ex.Message); }
            finally { streamlineSdkReleases.Enabled = !busy && dlssg.Checked; }
        }

        private static bool HasStreamlineRequired(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;
            string root = directory;
            string binX64 = Path.Combine(directory, "bin", "x64");
            if (Directory.Exists(binX64)) root = binX64;
            foreach (string name in InstallerEngine.GetDlssgRequiredFileNames())
                if (!File.Exists(Path.Combine(root, name))) return false;
            return true;
        }

        private static async Task<string> ExtractStreamlineSdkAsync(string zipPath, string destinationDirectory, CancellationToken token)
        {
            Directory.CreateDirectory(destinationDirectory);
            string[] required = InstallerEngine.GetDlssgRequiredFileNames();
            string[] extra = new[] { "nvngx_dlss.dll", "sl.dlss.dll", "sl.dlss_nr.dll", "sl.nis.dll", "nis.license.txt" };
            string[] licenses = new[] { "nvngx_dlss.license.txt", "reflex.license.txt" };
            var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in required) wanted[name] = "bin/x64/" + name;
            foreach (string name in extra) wanted[name] = "bin/x64/" + name;
            foreach (string name in licenses) wanted[name] = "bin/x64/" + name;
            wanted["license.txt"] = "license.txt";
            wanted["3rd-party-licenses.md"] = "3rd-party-licenses.md";
            using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
            {
                foreach (var pair in wanted)
                {
                    token.ThrowIfCancellationRequested();
                    System.IO.Compression.ZipArchiveEntry entry = null;
                    string tail = pair.Value;
                    foreach (var item in archive.Entries)
                    {
                        string n = item.FullName.Replace('\\', '/');
                        if (n.Equals(tail, StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith("/" + tail, StringComparison.OrdinalIgnoreCase))
                        { entry = item; break; }
                    }
                    if (entry == null) continue;
                    string destName = pair.Key == "license.txt" ? "Streamline-LICENSE.txt"
                        : pair.Key == "3rd-party-licenses.md" ? "Streamline-3rd-party-licenses.md"
                        : pair.Key;
                    string outPath = Path.Combine(destinationDirectory, destName);
                    using (var input = entry.Open())
                    using (var output = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[128 * 1024];
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                            await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                    }
                }
            }
            foreach (string name in required)
                if (!File.Exists(Path.Combine(destinationDirectory, name)))
                    throw new InvalidOperationException("Streamline SDK 解压后缺少必需文件：" + name);
            return destinationDirectory;
        }

        private InstallOptions ReadOptions()
        {
            string exePath = gameExe.Text.Trim().Trim('"');
            if (!File.Exists(exePath) || !string.Equals(Path.GetExtension(exePath), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择存在的游戏 EXE 文件。 ");
            string selectedModel = modelDll.Text.Trim().Trim('"');
            if (selectedModel.Length > 0 && !File.Exists(selectedModel))
                throw new InvalidOperationException("所选模型 DLL 不存在，请重新选择或清空后复用游戏目录中的模型。 ");
            string selectedFsr = fsrDirectory.Text.Trim().Trim('"');
            string selectedStreamline = streamlineDirectory.Text.Trim().Trim('"');
            bool installFsr = fsrEnabler.Checked || fsrNukem.Checked;
            bool installDlssg = dlssg.Checked;
            ReleaseInfo streamlineSdk = streamlineSdkReleases.SelectedItem as ReleaseInfo;
            bool hasLocalStreamline = HasStreamlineRequired(selectedStreamline);
            if (installDlssg && !hasLocalStreamline && streamlineSdk == null)
                throw new InvalidOperationException("已勾选 Streamline SDK：请点击「刷新 SDK」并选择版本，或填写含运行库的本地目录。 ");
            if (installFsr)
            {
                if (selectedFsr.Length == 0 || !Directory.Exists(selectedFsr))
                    throw new InvalidOperationException("已勾选 FSR 组件，请选择包含对应 DLL 的文件夹。 ");
                if (fsrEnabler.Checked && !File.Exists(Path.Combine(selectedFsr, "dlss-enabler-headless.dll")))
                    throw new InvalidOperationException("组件目录缺少 dlss-enabler-headless.dll（Enabler）。 ");
                if (fsrNukem.Checked && !File.Exists(Path.Combine(selectedFsr, "dlssg_to_fsr3_amd_is_better.dll")))
                    throw new InvalidOperationException("组件目录缺少 dlssg_to_fsr3_amd_is_better.dll（Nukem's）。 ");
            }
            return new InstallOptions
            {
                GameExe = Path.GetFullPath(exePath),
                ModelDll = selectedModel.Length == 0 ? null : Path.GetFullPath(selectedModel),
                ProxyName = (string)proxy.SelectedItem,
                Gpu = (string)gpu.SelectedItem,
                EnableNeuralRendering = neuralRendering.Checked,
                UseDlssInputs = useDlssInputs.Checked,
                FsrDirectory = installFsr ? Path.GetFullPath(selectedFsr) : null,
                InstallFsrEnabler = installFsr && fsrEnabler.Checked,
                InstallFsrNukem = installFsr && fsrNukem.Checked,
                StreamlineDirectory = installDlssg && hasLocalStreamline ? Path.GetFullPath(selectedStreamline) : null,
                StreamlineSdkRelease = installDlssg ? streamlineSdk : null,
                InstallDlssg = installDlssg,
                BridgeDllFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nvngx.dll_dlssnr.dll")
            };
        }

        private async Task InstallAsync()
        {
            await RunOperationAsync("正在准备安装…", async delegate(CancellationToken token)
            {
                InstallOptions options = ReadOptions();
                string zipPath = localZip.Text.Trim().Trim('"');
                ReleaseInfo release = releases.SelectedItem as ReleaseInfo;
                if (zipPath.Length == 0 && release == null)
                    throw new InvalidOperationException("请先选择上游并检查版本，或选择本地 ZIP。 ");
                if (zipPath.Length > 0 && !File.Exists(zipPath))
                    throw new InvalidOperationException("所选本地 ZIP 不存在。 ");
                string version = zipPath.Length > 0 ? "本地 ZIP：" + Path.GetFileName(zipPath) : release.Tag;
                WriteLog("目标 EXE：" + options.GameExe);
                if (release != null && !String.IsNullOrEmpty(release.SourceDisplay))
                    WriteLog("发布包来源：" + release.SourceDisplay + " · " + release.Tag + " — " + release.AssetName);
                WriteLog("安装版本：" + version + "；代理：" + options.ProxyName + "；GPU：" + options.Gpu);
                if (options.InstallFsrEnabler || options.InstallFsrNukem)
                    WriteLog("附加组件：" + (options.InstallFsrEnabler ? "Enabler " : "") + (options.InstallFsrNukem ? "Nukem's" : ""));
                if (options.InstallDlssg)
                    WriteLog("Streamline SDK：将写入游戏目录 OptiScaler\\streamline（不覆盖游戏 Engine 自带 Streamline）。");
                if (options.InstallDlssg && String.IsNullOrEmpty(options.StreamlineDirectory))
                {
                    if (options.StreamlineSdkRelease == null)
                        throw new InvalidOperationException("Streamline SDK 未选择版本，无法下载。 ");
                    string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "OptiScalerInstaller", "Cache");
                    string slZip = Path.Combine(cache, "streamline-sdk-" + options.StreamlineSdkRelease.Tag + ".zip");
                    progressText.Text = "正在下载 Streamline SDK…";
                    WriteLog("SDK 来源：" + options.StreamlineSdkRelease.SourceDisplay + " · " + options.StreamlineSdkRelease.Tag + " — " + options.StreamlineSdkRelease.AssetName);
                    IProgress<DownloadProgress> slReporter = new Progress<DownloadProgress>(ShowDownloadProgress);
                    string downloaded = await PackageService.DownloadAsync(options.StreamlineSdkRelease, cache, slReporter, token);
                    if (!String.Equals(downloaded, slZip, StringComparison.OrdinalIgnoreCase) && File.Exists(downloaded))
                    {
                        if (File.Exists(slZip)) File.Delete(slZip);
                        File.Copy(downloaded, slZip, true);
                    }
                    string slDir = Path.Combine(cache, "streamline-sdk-extract-" + options.StreamlineSdkRelease.Tag);
                    if (Directory.Exists(slDir)) try { Directory.Delete(slDir, true); } catch (IOException) { }
                    await ExtractStreamlineSdkAsync(slZip, slDir, token);
                    options.StreamlineDirectory = slDir;
                    WriteLog("SDK 已解压：" + slDir);
                }
                if (zipPath.Length == 0)
                {
                    string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "OptiScalerInstaller", "Cache");
                    progressText.Text = "正在下载发布包…";
                    IProgress<DownloadProgress> reporter = new Progress<DownloadProgress>(ShowDownloadProgress);
                    zipPath = await PackageService.DownloadAsync(release, cache, reporter, token);
                }
                token.ThrowIfCancellationRequested();
                progress.Style = ProgressBarStyle.Marquee;
                progressText.Text = "正在检查并安装…";
                string package = zipPath;
                await Task.Run(delegate { InstallerEngine.Install(package, options, version, WriteLog, token); }, token);
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = 100;
                progressText.Text = "安装完成";
                WriteLog("安装完成。请手动启动游戏，当前版本默认按 Insert 打开 OptiScaler 菜单；自定义热键以 OptiScaler.ini 为准。 ");
                if (options.InstallDlssg)
                    WriteLog("Streamline 已写入 OptiScaler\\streamline。请在游戏菜单中配置 OptiScaler 帧生成；安装完成不代表帧生成已经生效。 ");
                MessageBox.Show(this, "安装完成。\r\n\r\n请手动启动游戏，并按官方说明启用 Neural Rendering。", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, true);
        }

        private async Task UninstallAsync()
        {
            await RunOperationAsync("正在卸载并还原…", async delegate(CancellationToken token)
            {
                string selectedExe = gameExe.Text.Trim().Trim('"');
                if (!File.Exists(selectedExe)) throw new InvalidOperationException("请先选择原来安装时使用的游戏 EXE。 ");
                await Task.Run(delegate { InstallerEngine.Uninstall(selectedExe, WriteLog, token); }, token);
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = 100;
                progressText.Text = "卸载完成";
            }, true);
        }

        private Task RecoverAsync()
        {
            string selectedExe = gameExe.Text.Trim().Trim('"');
            if (!File.Exists(selectedExe))
            {
                WriteLog("请先选择游戏 EXE。 ");
                MessageBox.Show(this, "请先选择原来安装时使用的游戏 EXE。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return Task.CompletedTask;
            }
            return RunOperationAsync("正在恢复中断操作…", delegate(CancellationToken token)
            {
                InstallerEngine.Recover(selectedExe, WriteLog);
                progressText.Text = "恢复完成";
                return Task.CompletedTask;
            }, false);
        }

        private void OpenOfficialPage()
        {
            UpstreamSource source = SelectedSource();
            if (source == null)
            {
                WriteLog("请先选择上游仓库。 ");
                return;
            }
            string url = source.ReleasesPageUrl;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { WriteLog("无法打开浏览器：" + ex.Message + "；页面：" + url); }
        }

        private void FitToScreen()
        {
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int availableWidth = Math.Max(400, work.Width - 32);
            int availableHeight = Math.Max(400, work.Height - 32);
            MinimumSize = new Size(Math.Min(MinimumSize.Width, availableWidth), Math.Min(MinimumSize.Height, availableHeight));
            Size = new Size(Math.Min(Width, availableWidth), Math.Min(Height, availableHeight));
            Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
        }

        private void SaveSmokeScreenshot()
        {
            try
            {
                string destination = Path.GetFullPath(smokePath);
                string directory = Path.GetDirectoryName(destination);
                Directory.CreateDirectory(directory);
                using (Bitmap bitmap = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
                    bitmap.Save(destination, ImageFormat.Png);
                }
            }
            catch (Exception) { }
            finally { Application.Exit(); }
        }
    }
}
