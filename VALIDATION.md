# 交付验证记录

验证日期：2026-09-20，Windows x64。

当前版本 1.7.0：**Streamline 帧生成 SDK**
- 勾选后可从 `NVIDIA-RTX/Streamline` 官方 GitHub Release 下载 x64 SDK ZIP（校验 SHA-256；当前 v2.14.1，`streamline-sdk-v2.14.1.zip`）
- 从 SDK 的 `bin/x64` 提取：`sl.interposer.dll`、`sl.common.dll`、`sl.dlss_g.dll`、`sl.reflex.dll`、`sl.pcl.dll`、`nvngx_dlssg.dll`
- 写入游戏目录 **`OptiScaler\streamline\`**（启用 OptiScaler 自身帧生成）
- **不覆盖**游戏 Engine 自带 Streamline；也不写游戏根目录
- 可选：填写本地目录（支持扁平目录或解压后的 SDK `bin/x64` 布局）

路径文本框已用真实屏幕像素核对：框内文字垂直居中、边框完整、与下拉框同高。

测试：核心 54、下载 22、UI 8 通过。EXE **1.7.0.0** SHA-256 `1d7ed0aa9db9b300790127bfabc986fe32e94e5f23c365852da04e7336018e2c`。便携包 `dist\OptiScaler-DLSSNR-Installer-1.7.0-win-x64.zip` SHA-256 `ea277a539cb64b5ec253b407af2f4201f79912a9383397c9f8acf4f7b70ebfae`。

**快捷方式与管理员启动**（已配置，脚本 `setup-shortcuts-admin.ps1`）：
- 开始菜单（所有用户）：`C:\ProgramData\Microsoft\Windows\Start Menu\Programs\OptiScaler DLSSNR 安装助手.lnk`
- 当前用户开始菜单与桌面快捷方式已同步指向安装目录 EXE
- 机器级管理员启动：`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`  
  `"D:\Program Files\OptiScaler DLSSNR Installer\OptiScaler-DLSSNR-Installer.exe"="RUNASADMIN"`（对所有用户生效）

以下为历史版本验证记录。

1.6.0：移除 RTXMFG 选项；上游默认留空；FSR 始终显示；表单网格对齐。
- 界面去掉「星级更高 / 仅某某上游显示」类文案
- **移除 RTX 40 MFG · RTXMFG 选项**（UI 不再提供；引擎路径保留但安装时恒为关闭）
- **上游仓库默认留空**，需用户手动选择后再检查版本
- FSR：选择 Dagherbou · DLSSNR 后可勾选；Streamline 兼容修复：始终可勾选

`MainForm.cs` 在本轮曾被误截断，已按功能契约完整重建并通过全套测试。

测试：核心 54、下载 22、UI 7 通过。EXE `1.6.0.0` 已部署，SHA-256 `a8038422c98251b088acb6cc2d7f70881b9bf77a600ff05e76b9e93510e0722d`。便携包 `dist\OptiScaler-DLSSNR-Installer-1.6.0-win-x64.zip`，SHA-256 `c5a752736b45830d6d094688ed8f569aa340fc095b8eb0b7b913b23793fc2119`。

以下为历史版本验证记录。

1.5.3：鸣潮 RTXMFG `version.dll` 双注入导致启动变慢，已删除；安装器曾加警告。

1.5.2：RTXMFG 可从 GitHub 下载到安装器目录；FSR 无法从 GitHub 拉取。

1.5.1：FSR=仅 Dagherbou；Streamline=始终显示（兼容修复）；RTXMFG=仅 PreSR。

1.5.0：曾将 FSR/Streamline/RTXMFG 全部限制在 PreSR；1.5.1 按用户要求改为上表规则。

1.4.2：PreSR 包缺桥接 DLL 时回退安装器目录文件。

1.4.2：PreSR 包缺 `nvngx.dll_dlssnr.dll` 时自动回退安装器目录桥接文件。

1.4.1：社区包内非安装资产（图片/脚本等）跳过，不再整包失败。

1.4.0：双上游可选（Dagherbou / wilsjo2 PreSR-Multipass）。

1.4.0：在线下载增加上游仓库可选——默认 `Dagherbou/OptiScaler_DLSSNR`，可选 `wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass`。版本列表、下载校验与「打开官方页面」绑定当前所选上游；仍只允许这两个 GitHub 仓库的 HTTPS Release ZIP（需官方 SHA-256）。本地 ZIP 优先时禁用上游/版本控件。Streamline 仍为 1.3.0 的社区根目录白名单安装。

1.3.0：Streamline 可选项改为社区做法——白名单文件写入**游戏 EXE 同级根目录**并备份原文件。必需 6 个 DLL 仍必选；源目录若存在则额外复制 `nvngx_dlss.dll`、`sl.dlss.dll`、`sl.dlss_nr.dll`、`sl.nis.dll`、`nis.license.txt` 及两个原有许可文件。不再创建 `OptiScaler\streamline`。v1.2 的 `OptiScaler\streamline` 记录在未重新勾选时仍会保留；建议先卸载再按新版安装。

1.3.0 本地验证（2026-09-12/13）：核心安装/事务测试 48 项、下载/缓存测试 19 项、UI 选项测试 4 项全部通过；x64 EXE 编译成功，版本 1.3.0.0。

1.3.0 已部署到 `D:\Program Files\OptiScaler DLSSNR Installer`（管理员受控脚本 `deploy-v1.3.ps1`，失败回滚可用）。12 个白名单文件与 `dist` 源哈希逐项一致；仅 EXE 与 `使用说明.md` 相对 1.2.0 发生变更。`nvngx_dlssnr.dll` = `e67dee209320cdafe0e93e45675d7aa34323a53acc57a72b2e40a181581c989a`，`nvngx.dll_dlssnr.dll` = `17cf51d25d142d0be15fcac945cc928c37849a1cb61011cb9c742d72df86647c`，均未改变。桌面快捷方式目标与工作目录仍指向安装目录。旧文件备份：`build\deployment-backups\2863f332eb6140fb9f8b7f0a385c1697`。

便携包 `dist\OptiScaler-DLSSNR-Installer-1.3.0-win-x64.zip` 共 13 个文件（EXE、说明、FSR 2 DLL、Streamline 6 DLL、2 许可、SHA256SUMS；不含 NR 模型），14,078,806 字节，SHA-256 `154a82938429c7d5bfa95b628739c002e013c6fef490b78fcbfd9f11bafa1b05`。包内各文件与 `SHA256SUMS.txt` 一致。安装版 EXE SHA-256：`dc35a0a8a0412a348e3059fd8549d1c4c28554e95d5739e6c10494831ae2c1ab`。

1.3.0 限制：尚未在真实游戏中验证启动或帧生成；若游戏目录仍有 v1.2 写入的 `OptiScaler\streamline`，需用户自行卸载重装才会迁到根目录。

以下为历史版本验证记录。

1.2.0：新增默认关闭的“DLSS 帧生成 · Streamline”可选项，六个必需 DLL 和两个可选许可文件按白名单部署到游戏 EXE 旁的 `OptiScaler\streamline`。不修改 FrameGen 的开关、输入、输出或 Nvngx 替换设置。

1.2.0 验证：核心测试 47 项、下载测试 19 项、无可见窗口的 UI 选项测试 4 项全部通过；另用本地官方 v0.2.0 ZIP 完成临时目录安装、原始配置及无关文件保留、卸载还原检查。最终 x64 编译成功，无警告。新增测试覆盖逐个缺件、错误 PE/32 位/EXE、许可组合、源目录白名单、与 FSR 共存、首次备份、后续更新保留、取消勾选不接管手动文件、失败不部分提交、隐藏非法路径忽略及操作期间禁用输入。

1.2.0 已部署到 `D:\Program Files\OptiScaler DLSSNR Installer`，EXE SHA-256 为 `d7f54a7ad26b84732cf1081a82f658db70cf23898c2af51303fb72b3094b4baa`。安装目录的 12 个文件已独立与计划逐项校验，原 NR 模型、桥接 DLL 哈希不变，桌面快捷方式目标和工作目录正确。更新备份在 `build\deployment-backups\e1c5edd584bd45e5a4783f55ab544567`。

便携包 `dist\OptiScaler-DLSSNR-Installer-1.2.0-win-x64.zip` 共 13 个文件（包含校验清单，包含 FSR/Streamline，不包含 NR 模型），14,602,040 字节，SHA-256 为 `ddf6741bc6d26b5193c4a08bff2862912deace1414fb5fbaf9493b4659d8d057`；包内各文件均与 dist 对应文件哈希一致。Streamline 使用用户本地提供的资源，六个 DLL 的 NVIDIA Authenticode 签名有效；五个 sl DLL 为 2.13.0-beta10，nvngx_dlssg.dll 为 310.8.0.0。

1.2.0 限制：本轮没有操作两个真实游戏目录，没有调用电脑控制或打开界面。UI 选项逻辑已验证，新三项布局未进行真实窗口视觉复核；游戏内 Streamline 兼容性及帧生成实际效果仍未验证。安装运行库不等于帧生成已启用成功。

以下为历史版本验证记录。

1.1.0 可选 FSR 组件更新：核心/事务测试 28 项、下载/缓存测试 19 项通过。新增覆盖两项组件单选和双选安装、普通更新保留、未勾选不接管用户文件、首次备份与卸载还原、缺失/错误格式源拒绝且无部分提交。另通过隐藏窗口的 ReadOptions 检查：取消所有选项后忽略隐藏的非法目录，勾选时拒绝无效目录。最终编译无警告。

1.1.0 已部署至 `D:\Program Files\OptiScaler DLSSNR Installer`，EXE SHA-256 为 `d62c1958ac401d8ae2fc025e601fa852b3d13770d97ff4dc08a8fad76c2d19e5`。独立核对安装版四个更新文件及便携 ZIP 内对应文件全部一致；桌面快捷方式目标、工作目录正确，原 NR 模型及桥接 DLL 哈希不变。备份位于 `build\deployment-backups\260f39c48caa44bfbec10441a57632b1`。此次未操作两个实际游戏目录，也未再调用电脑控制工具。

界面验证范围：已查看可选项默认关闭时的真实窗口，并确认勾选后组件目录出现在控件树中；完整展开界面的视觉复核因用户停止电脑控制而中止。最终部署通过文件、版本及组件校验完成。

- `.\build.ps1 -Test -OfficialZip 'E:\BaiduNetdiskDownload\DLSS 5\OptiScaler-DLSSNR\OptiScaler-DLSSNR-v0.2.0.zip'`：文件引擎 17 项、下载服务 19 项通过，0 失败。
- 真实官方 API 查询成功，默认选中官方 Latest `v0.2.0-dlssnr`。
- 真实下载模块完整获取 130,486,024 字节官方 ZIP；SHA-256 `8eece7a4d7de6de5917f0c99ac60540b2d77022e7699bba717b0a6d9e1829bce`，独立复核一致。
- 真实下载收到 1,454,080 字节后取消，子进程结束且无 ZIP/part 残留；中文、空格路径成功。
- 使用用户本地 v0.2.0 ZIP 和解压的模型 DLL 在临时目录执行完整安装、INI 启用、卸载、原始配置/无关存档保持检查，通过。模型未加载或执行。
- 进程在文件提交期间强制退出后的恢复测试通过；写入失败回滚、并发修改冲突、损坏备份、压缩包越界、代理冲突等回归通过。
- 初版 x64 WinForms EXE 的 `--ui-smoke` 返回 0，但该 DrawToBitmap 预览未能覆盖真实 WM_PAINT 重绘，遗漏了用户随后反馈的按钮黑边、串字及日志半行问题。
- UI 修复版编译成功、无编译警告。独立真实 WM_PAINT 探针确认 Button 的 Opaque 标志会跳过背景绘制；清除该标志并完整清底，文字保留裁剪及坐标变换后，真实窗口按钮显示正常。
- 日志收起时显示最新消息，展开时保留完整历史；真实窗口已检查展开及版本查询过程中的按钮禁用、恢复和日志更新。安装版也已实际启动并查看窗口，自动填入同目录模型路径。
- 此前 1.0.0 热键修正版的安装版、dist 和 ZIP 内 EXE SHA-256 均为 `d328269d80fef1085b01ec275c606808f432b29cb581c958ef1f4f4e05d04c9d`；该版本已由上述 1.1.0 替换。初版 EXE 备份在 `build\ui-update\installer-before-ui-fix.exe`。

后续热键排查：用户自行安装至鸣潮后，只读检查实际游戏配置，确认 v0.2.0 的 `[Menu] ShortcutKey=auto` 对应 Insert（0x2D），Home 为旧版默认键。游戏当前日志持续出现 `DlssNr_Dx12::Dispatch` 耗时记录，表明该次运行已加载并执行 NR。已更正安装器页脚、完成日志、完成弹窗及说明文档；编译及安装版/ZIP 哈希校验通过，未关闭运行中的游戏或修改游戏配置。

限制：实际游戏中的菜单按键响应、NR 画面效果或其他显卡兼容性尚未验证。用户模型的 Authenticode 状态为 HashMismatch，具体修改内容和来源真实性未核验。视觉已验证当前屏幕尺寸，未逐一验证所有 DPI/多屏组合。

原 .NET HTTP 通道在本环境遭遇 Windows SSPI 错误，已改为优先调用系统自带 curl；该实际交付通道已通过上述在线测试。保留的 HttpClient 后备未在本机验证在线成功。
