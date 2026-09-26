<div align="center">

# OptiScaler DLSSNR 安装助手

**中文一键安装、备份与还原 OptiScaler DLSS Neural Rendering**

[![GitHub](https://img.shields.io/badge/GitHub-181717?logo=github&logoColor=white)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer)
[![Stars](https://img.shields.io/github/stars/H0NG1Y/optiscaler-dlssnr-installer?color=yellow&label=stars&logo=github)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/stargazers)
[![Downloads](https://img.shields.io/github/downloads/H0NG1Y/optiscaler-dlssnr-installer/total?color=orange&label=downloads)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/releases/latest)
[![Windows Download](https://img.shields.io/github/v/release/H0NG1Y/optiscaler-dlssnr-installer?color=brightgreen&label=Windows%20Download&logo=windows&logoColor=white)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/releases/latest)
[![C#](https://img.shields.io/badge/C%23-.NET-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)

</div>

## 项目介绍

Windows 10/11 x64 中文便携安装器，供 [Dagherbou/OptiScaler_DLSSNR](https://github.com/Dagherbou/OptiScaler_DLSSNR) 使用。双击 `OptiScaler-DLSSNR-Installer.exe` 即可，无需安装 Python、PowerShell 模块或另外下载 .NET SDK。使用 Windows 的 .NET Framework 4.8 和系统自带的 HTTPS 下载工具；精简系统若移除了相关组件需先恢复组件，或选择本地官方 ZIP。

安装器只做三件事：把你选择的上游组件写进游戏目录、写之前先备份、之后随时可以“卸载并还原”。它默认以普通权限运行，不修改系统注册表、服务、驱动或启动项，也不会自动启动游戏。当前版本 **1.7.0**。

## 下载

请从 [GitHub Releases](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/releases/latest) 下载最新版本（1.7.0）。

- 推荐 `OptiScaler-DLSSNR-Installer-Setup-v1.7.0.msi`：安装到 `C:\Program Files\OptiScaler DLSSNR Installer`（向导里可以改成别的目录），创建开始菜单与桌面快捷方式，写入 Windows 卸载信息，并给主程序加上「以管理员身份运行」兼容标记 —— 助手要往游戏目录写文件，必须提权。装完后可在「设置 → 应用」里正常卸载。
- 免安装版 `OptiScaler-DLSSNR-Installer-v1.7.0.exe`：单文件，双击直接运行，不写安装信息、不建快捷方式。
- 两个包都附 `checksums.txt` 与 `.sha256`，可用于核对下载文件的 SHA-256。

公开的安装包**不包含**下列文件，它们要么需你自备，要么由界面上的「在线下载 / 刷新」从项目支持的 GitHub 上游仓库临时获取：

| 文件 | 是什么 | 怎么得到 |
| --- | --- | --- |
| `nvngx_dlssnr.dll` | DLSS-NR 神经渲染模型（160 MB 级） | 属本机素材、不作为公开发行内容，请自备并在界面中选择 |
| `nvngx.dll_dlssnr.dll` | 上游桥接文件 | 点「在线下载」拉取上游官方包时一并获取；若用本地 ZIP，需该 ZIP 自带 |
| `dlss-enabler-headless.dll`、`dlssg_to_fsr3_amd_is_better.dll` | 可选 FSR 帧生成组件 | 需自备，在界面中指定本地 FSR 目录 |
| Streamline 的 6 个 DLL | 可选 DLSS 帧生成组件 | 用安装器内置的「刷新 SDK」从 NVIDIA-RTX/Streamline 官方 Release 下载并校验 SHA-256 |

也就是说：能从官方 GitHub Release 拿到的部分（上游 OptiScaler 包、Streamline SDK）由安装器在线下载并校验；属于本机素材或上游不随包分发的部分，一律由你自己提供。

## 使用方法

1. 先退出游戏，再选择**实际运行游戏的 EXE**。某些游戏应选择 `Binaries\Win64` 中的程序，不能只选择启动器。
2. **上游仓库**默认为空，请先选择 **Dagherbou · DLSSNR** 或 **wilsjo2 · PreSR-Multipass**，再点「检查版本」。下载地址仅允许这两个仓库的 GitHub Release ZIP（需官方 SHA-256）。也可使用本地 ZIP；填写本地 ZIP 时优先使用本地包。
3. 选择本机的 **`nvngx_dlssnr.dll`**。如果安装器旁边存在同名模型，会自动填入该路径；若希望复用游戏 EXE 旁边已有的模型，可以清空模型栏。暂时没有模型时，可以取消“启用 Neural Rendering”以只安装 OptiScaler。
4. **附加与兼容修复**：
   - **Streamline 兼容修复**：始终可勾选。仅在游戏启动异常、帧生成冲突或需替换游戏本地 Streamline 时使用；写入游戏 EXE 同级根目录并备份原文件。
   - **FSR 3 · Enabler / Nukem's**：始终可选，与所选上游无关。DLL 须本地提供。
5. 通常保持代理 `dxgi.dll`。按照官方脚本，Vulkan 游戏建议试 `winmm.dll`，XGP / Microsoft Store 游戏可试 `winmm.dll` 或 `version.dll`。`OptiScaler.asi` 需要游戏中已有 ASI 加载器。
6. 选择显卡并点击“安装 / 更新”。完成后手动启动游戏，当前 v0.2.0 默认按 **Insert（Ins）** 打开 OptiScaler 菜单检查 Neural Rendering。Home 是旧版默认热键；若使用旧版本或自定义热键，以游戏目录 `OptiScaler.ini` 的 `[Menu] ShortcutKey` 为准。

模型不包含在这个安装器或上游发布包中；`nvngx.dll_dlssnr.dll` 是上游桥接文件，不能代替模型。当前已核对的 v0.2.0 官方说明要求驱动至少 **616.56**，Blackwell 之前的架构需要兼容的修改版模型。安装器仅验证 DLL 的 x64 格式，不验证其模型版本、来源、显卡兼容性或画质效果；请以所选版本的[官方发布说明](https://github.com/Dagherbou/OptiScaler_DLSSNR/releases)为准。

## 功能

- 中文 WinForms 界面，异步执行带进度与日志，准备阶段和下载可随时取消
- 只连接项目支持的上游仓库：**Dagherbou · DLSSNR** 与 **wilsjo2 · PreSR-Multipass**，校验资产长度与 GitHub 提供的 SHA-256，缓存每次使用前重新校验
- 支持已下载的本地官方 ZIP，并检查包结构、路径与必需文件（本地包**不代表**已完成官方来源认证）
- 代理文件可选 `dxgi.dll`、`winmm.dll`、`version.dll`、`dbghelp.dll`、`d3d12.dll`、`wininet.dll`、`winhttp.dll`、`OptiScaler.asi`；显卡可选 NVIDIA / AMD / Intel
- 保留原有 INI 设置，只修改本次选择所需的 `[DlssNr] Enabled`；AMD/Intel 根据“使用 DLSS 输入”设置 `[Spoofing] Dxgi`，NVIDIA 保留该项
- 不安装可选 OptiPatcher，不执行上游批处理、注册表或 Linux 脚本
- 提交文件前先备份并记录清单，失败自动回滚；异常退出后可用「恢复中断操作」继续
- 一键「卸载并还原」，只处理本工具清单中的文件，不按通配符删除整个游戏文件夹
- 可选 FSR / DLSS 帧生成组件与 Streamline SDK 刷新，详见下文

### 可选 FSR 帧生成组件

FSR 目录中只需包含所勾选组件的 DLL，文件名必须如下。安装器将其按原名复制到**游戏 EXE 同级的 `OptiScaler` 子目录**；“放入 OptiScaler 文件夹内”等空提示文件无需复制。

| 安装器选项 | 本地 DLL 文件名 | OptiScaler v0.2.0 菜单选项 |
| --- | --- | --- |
| FSR 3 多帧生成 · Enabler | `dlss-enabler-headless.dll` | `Enabler`（FSR 3 MFG） |
| FSR 3 帧生成 · Nukem's | `dlssg_to_fsr3_amd_is_better.dll` | `Nukem's`（FSR 3 FG） |

这两个文件可以共存，使用时在 OptiScaler 菜单中选择其中一种；同时勾选安装不表示两种帧生成同时运行。安装器不修改 `FGInput`、`FGOutput`、`FGNvngxReplacement` 或帧生成开关，已有帧生成设置保持原样。默认 `[Libraries] OptiDllPath=auto` 对应上述 `OptiScaler` 子目录；若曾自定义此路径，请确认实际加载目录与文件位置一致。

对于已有可用 DLSS 帧生成输入的游戏，可按 **Insert** 打开菜单，在 `Frame Generation` 中将 `FG Input` 设为 `DLSSG via Nvngx`，再将 `FG Nvngx` 设为 `Enabler` 或 `Nukem's`，点击 `Save Settings` 并重启游戏，同时在游戏设置中开启 DLSS 帧生成。菜单和支持情况以所安装的上游版本及游戏为准；安装成功不代表游戏一定支持该模式。

可选 DLL 由用户本地提供，安装器不下载这两个组件，也不验证其发布来源或实际帧生成效果。上述文件名和菜单对应关系已按上游 [v0.2.0 加载器](https://github.com/Dagherbou/OptiScaler_DLSSNR/tree/v0.2.0-dlssnr/OptiScaler/framegen/nvngx)及[菜单定义](https://github.com/Dagherbou/OptiScaler_DLSSNR/blob/v0.2.0-dlssnr/OptiScaler/menu/menu_common.cpp#L3205)核对。

### 可选 DLSS 帧生成组件

“DLSS 帧生成 · Streamline”按社区做法，把 Streamline 组件**写入游戏 EXE 同级根目录**，并备份已有同名原文件。安装器不会按源文件夹里的空提示文件无差别覆盖；只处理下列白名单。

**必需 6 个 DLL**（源目录必须齐全）：

```text
sl.interposer.dll
sl.common.dll
nvngx_dlssg.dll
sl.dlss_g.dll
sl.reflex.dll
sl.pcl.dll
```

**源目录若存在则一并复制**（社区包常见额外文件）：

```text
nvngx_dlss.dll
sl.dlss.dll
sl.dlss_nr.dll
sl.nis.dll
nis.license.txt
nvngx_dlss.license.txt
reflex.license.txt
```

不会复制源目录中的其他任意文件，也不会创建 `OptiScaler\streamline` 子目录。若你此前用旧版装到过 `OptiScaler\streamline`，建议先“卸载并还原”，再按本版勾选安装到游戏根目录。

没有本地 Streamline 目录时，可点「刷新 SDK」从 **NVIDIA-RTX/Streamline** 官方 Release 选择版本，安装器下载 ZIP 并校验 SHA-256 后提取所需的 6 个 DLL。安装后仍需根据显卡、驱动和游戏支持情况，在 OptiScaler 菜单中选择帧生成输入与输出，保存设置并重启游戏。安装完成只表示组件已写入并完成备份，实际帧生成是否生效需在游戏中确认。

### 更新与还原

- 在线下载仅使用此项目的官方 GitHub Release，校验资产长度与 GitHub 提供的 SHA-256；缓存每次使用前重新校验。本地 ZIP 会检查包结构、路径与必需文件，**不代表已完成官方来源认证**。
- 保留原有 INI 设置，只修改本次选择所需的 `[DlssNr] Enabled`；AMD/Intel 根据“使用 DLSS 输入”设置 `[Spoofing] Dxgi`，NVIDIA 保留该项。不安装可选 OptiPatcher，不执行上游批处理、注册表或 Linux 脚本。
- 安装会记录所写文件和原始备份。更新仍使用第一次安装前的备份基线；更换代理名称须先卸载旧版本。
- 已由本工具安装的 FSR / DLSSG 可选组件及随附许可文件在后续更新时保留；取消勾选表示本次不新增或替换该组件，不会单独卸载已有组件，也不接管手动放入但未由本工具管理的文件。重新勾选并提供本地组件可更新对应文件，“卸载并还原”会将已管理文件一并按首次安装前的备份还原；安装前不存在的文件则移除。
- 已有代理文件不直接覆盖。若检测到非本工具管理的 OptiScaler，应先按原有方式卸载。对 ReShade 等组件的兼容性须按具体游戏验证。
- 点击“卸载并还原”只处理本工具清单中的文件，不按通配符删除整个游戏文件夹。被其他工具替换过的 DLL 会触发冲突，整个操作停止。
- 修改过的 `OptiScaler.ini` 在卸载时另存到 `.optiscaler-dlssnr-installer\saved-settings` 后再还原。原始备份保留在同目录下的 `originals`。
- 准备阶段和下载可取消；文件提交阶段会先完成或回滚。异常退出后使用“恢复中断操作”。不要在安装期间运行游戏更新器，也不要手动修改安装记录。
- 运行游戏产生的日志、后来添加的插件等未记录文件不会自动删除。成功卸载后若不再需要备份/另存配置，可自行删除 `.optiscaler-dlssnr-installer`；仍在使用安装器管理安装时请保留它。

## 常见问题

**游戏目录写不进去，提示权限不足？**

如果游戏目录受 Windows 权限保护，请关闭工具后右键“以管理员身份运行”。工具默认以普通权限运行，不修改系统注册表、服务、驱动或启动项，也不会自动启动游戏。请遵从目标游戏的 Mod 规则。

**手里没有 `nvngx_dlssnr.dll` 模型，还能用吗？**

可以。取消勾选“启用 Neural Rendering”，安装器只装 OptiScaler；之后拿到模型再重装或更新即可。模型不随安装器或上游发布包分发，需自备。

**上游仓库该选哪个？**

默认为空，必须先自己选：**Dagherbou · DLSSNR** 提供 DLSSNR 方案，**wilsjo2 · PreSR-Multipass** 是另一个可选上游。只有这两个仓库的 GitHub Release ZIP 会被当作在线下载来源，也都需要官方 SHA-256。

**游戏里按哪个键打开 OptiScaler 菜单？**

当前 v0.2.0 默认为 **Insert（Ins）**，Home 是旧版默认热键。若使用旧版本或自定义热键，以游戏目录 `OptiScaler.ini` 的 `[Menu] ShortcutKey` 为准。

**代理文件选哪个？**

通常保持 `dxgi.dll`。按官方脚本，Vulkan 游戏建议试 `winmm.dll`，XGP / Microsoft Store 游戏可试 `winmm.dll` 或 `version.dll`；`OptiScaler.asi` 需要游戏中已有 ASI 加载器。更换代理名称前须先卸载旧版本。

**卸载会不会删掉游戏里的其他文件？**

不会。“卸载并还原”只处理安装清单中的文件，被其他工具替换过的 DLL 会触发冲突并停止操作。运行游戏产生的日志、后来添加的插件等未记录文件都不会被自动删除。

**怎么手动核对安装包？**

在 PowerShell 中运行 `Get-FileHash .\OptiScaler-DLSSNR-Installer-Setup-v1.7.0.msi -Algorithm SHA256`（免安装版换成对应的 EXE 文件名），再与同一 Release 中 `.sha256` 或 `checksums.txt` 的值比较。

## 源码结构

- `src\`：C# 源码 —— `Program.cs`、`MainForm.cs`、`ModernControls.cs`、`InstallerEngine.cs`、`PackageService.cs`、`Contracts.cs`、`CurlTransport.cs`、`AssemblyInfo.cs`（版本 1.7.0）
- `installer\`：WiX v3 的 MSI 定义与文档 —— `Installer.wxs`、`WixUI_zh-CN.wxl`、`MSI-安装说明.md`、`inspect-msi-tables.ps1`、`test-msi-install.ps1`、`README.md`
- `build.ps1`：编译主程序（输出 `dist\OptiScaler-DLSSNR-Installer.exe`）；`-Test` 会编译并运行三套测试
- `build-msi.ps1`：用 WiX 3.14 生成 MSI
- `tests\`：测试源码
- `assets\`：图标等资源；`app.manifest`：清单文件
- `README.md`：本文件，同时作为安装包内的 `使用说明.md`

源码分为独立的文件引擎、GitHub 下载服务和 WinForms 界面；不依赖第三方 NuGet 包。测试使用临时目录，不针对你的真实游戏。

```powershell
.\build.ps1
.\build.ps1 -Test
# 可选：用已经下载的官方 v0.2.0 完整包测试临时游戏目录
.\build.ps1 -Test -OfficialZip '完整 ZIP 路径'
```

## 免责声明

- 这是独立制作的安装助手，**并非上游官方安装器**，与 OptiScaler 及各上游仓库作者无隶属关系。发行 EXE 不包含上游 DLL，其下载的上游包及许可仍属于各自作者。
- 安装器仅验证 DLL 的 x64 格式，不验证其模型版本、来源、显卡兼容性或画质效果；支持情况请以所选版本的官方发布说明为准。
- DLSS-NR 模型属本机素材，上游明确不随包分发，请勿再次分发；公开安装包同样不含该模型。
- 使用本工具修改游戏目录属于个人行为，请遵从目标游戏的 Mod 规则；因使用本工具产生的任何后果由使用者自行承担。

[English README](README.en.md)
