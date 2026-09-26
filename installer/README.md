# MSI 打包说明（开发向）

这个目录是 1.7.0 便携版之外的**第二条发布通道**：把安装助手打成 perMachine 的 Windows Installer 包，
交给用户双击安装、从“设置 → 应用”卸载。**MSI 只往安装目录写文件，不碰任何游戏目录**——
往游戏目录写组件始终由助手程序自己在运行时完成。

## 文件

| 文件 | 作用 | 是否手工维护 |
| --- | --- | --- |
| `Installer.wxs` | WiX v3 主定义（产品、目录、组件、快捷方式、注册表、UI） | 是 |
| `WixUI_zh-CN.wxl` | WiX 内置 UI 的简体中文本地化（561 条字符串，`Codepage="936"`） | 否，来自 wixtoolset/wix3 |
| `License-zh-CN.txt` | 许可页文本模板，含 `{{VERSION}}` 占位符 | 是 |
| `MSI-安装说明.md` | 随 MSI 装到安装目录的说明（用户向） | 是 |
| `test-msi-install.ps1` | 真实安装/卸载回归测试（需要 UAC 提权） | 是 |
| `..\build-msi.ps1` | 构建 + 自校验脚本 | 是 |
| `..\build\msi\License.rtf` | 由 `License-zh-CN.txt` 转义生成 | 否，构建产物 |
| `..\build\msi\msi-build-result.json` | 构建记录（载荷哈希、MSI 哈希、ProductCode、验证结果） | 否，构建产物 |

## 构建

```powershell
# 直接跑（若系统执行策略限制，用 powershell -ExecutionPolicy Bypass -File）
.\build-msi.ps1                 # 产出 dist\OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi
.\build-msi.ps1 -StageOnly      # 只整理载荷 / 生成 RTF，不编译
.\build-msi.ps1 -RebuildExe     # 先用 build.ps1 重编助手 EXE 再打包
.\build-msi.ps1 -NoModel        # 不含 158 MB 模型，产物自动加 -nomodel 后缀
.\build-msi.ps1 -NoFsr          # 不含 FSR 两个 DLL，产物加 -nofsr 后缀
.\build-msi.ps1 -NoVerify       # 跳过管理安装解包比对与 dark 反编译
```

工具链取自 NuGet 包 `wix 3.14.1`（`https://api.nuget.org/v3-flatcontainer/wix/3.14.1/wix.3.14.1.nupkg`，
sha256 `15D50463C73DCE31FBEA5440AC33AF47E92D54D4188166D207E9E39577B8FE0F`），解压在
`build\tools\wix314\tools\`（`candle.exe` / `light.exe` / `dark.exe` / `WixUIExtension.dll` / `WixUtilExtension.dll`）。
这些二进制没有 Authenticode 签名，来源与哈希就以上面这行为准。**不联网也能构建**（工具链已落地）。

## 包内载荷与来源

| 装到安装目录的文件 | 来源 | 校验 |
| --- | --- | --- |
| `OptiScaler-DLSSNR-Installer.exe` | `dist\OptiScaler-DLSSNR-Installer.exe` | 与 `dist\SHA256SUMS.txt` 一致 |
| `使用说明.md` | `dist\使用说明.md` | 与 `dist\SHA256SUMS.txt` 一致 |
| `MSI-安装说明.md` | `installer\MSI-安装说明.md` | 构建时计算 |
| `SHA256SUMS.txt` | 构建时按上面这些文件生成 | 自身不入清单（不自我引用） |
| `nvngx.dll_dlssnr.dll` | 上游官方包 `OptiScaler-DLSSNR-v0.2.0.zip` 内的同名文件 | `17CF51D2…647C`（114,688 字节） |
| `nvngx_dlssnr.dll` | 本机素材 `local-assets\nvngx_dlssnr.dll`（310.8） | `E67DEE20…C989A`（165,840,496 字节） |
| `FSR\dlss-enabler-headless.dll` | `dist\FSR\` | `b2d1dbbe…abcf` |
| `FSR\dlssg_to_fsr3_amd_is_better.dll` | `dist\FSR\` | `806020c0…4b5e` |

**没有打包**（故意的）：

- `streamline\`：`NVIDIA-RTX/Streamline` 官方 Release 就能下，助手 1.7.0 自带“刷新 SDK”会自己取，符合“可从 GitHub 下载的就不塞进包里”。
- `RTXMFG\`：1.6.0 已移除该功能，目录里那份是历史残留。
- NR 模型属本机素材（上游明确不随包分发），因此 **MSI 只在本机自用，不要再往外发**。

## 构建脚本自校验做了什么

1. `msiexec /a "<msi>" /qn TARGETDIR=<临时目录>` 管理安装解包，把解出来的文件按 **SHA-256 多重集**与载荷清单比对（不按文件名比，因为管理安装可能写出 8.3 名）。解包目录里会多一份 `msiexec` 自带的精简镜像 MSI，按文件名排除。
2. `dark.exe -x -o decompiled.wxs` 反编译，打印 `Shortcut` / `RegistryValue` / `Feature` / `CustomAction` / `Condition` / `Publish` 行和 `ProductCode`，人工核对表内容。

## 踩过的坑（改 wxs / ps1 前先看这里）

- **必须带 UTF-8 BOM**：本机只有 Windows PowerShell 5.1，它按 ANSI(GBK) 读无 BOM 的脚本，中文会把引号吞掉，报一堆
  `表达式或语句中包含意外的标记`。`write`/`edit` 工具写出来的文件没有 BOM，**每次编辑后都要补**：
  `[IO.File]::WriteAllText($p, [IO.File]::ReadAllText($p,(New-Object Text.UTF8Encoding($false))), (New-Object Text.UTF8Encoding($true)))`。
  `.wxs` 同样加 BOM（XML 声明是 UTF-8，且 candle 读文件要看编码）。
- **快捷方式必须嵌在 `<File>` 里**：嵌在 `<Component>` 上时 WiX 会把 Shortcut 表的 `Target` 写成 `[INSTALLFOLDER]`，
  快捷方式点开只是打开文件夹；嵌进 `<File>` 才会写成 `[#InstallerExe]`，指向 EXE。
- **快捷方式目录只用标准属性 `ProgramMenuFolder` / `DesktopFolder`**：perMachine + `ALLUSERS=1` 时它们解析为
  “所有用户”的开始菜单 `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\` 与 `C:\Users\Public\Desktop\`（有安装日志为证）。
  `CommonProgramsFolder` / `CommonDesktopFolder` 是**陷阱**：MSI 不认这两个名字，实测解析成源盘根目录 `D:\`
  （日志原文 `Adding CommonDesktopFolder property. Its value is 'D:\'`），快捷方式被悄悄建到 `D:\` 根目录、卸载再从那儿删掉，
  表面看像“卸载没清干净”，实际是 MSI 从没碰过正确路径。**是跑真实安装回归才发现的，勿改回去。**
- 用 `ProgramMenuFolder` / `DesktopFolder` 会触发三条 ICE：`ICE43`（非通告快捷方式要有 HKCU KeyPath）、
  `ICE57`（同一组件混了每用户/每机器数据 + 每机器 KeyPath）、`ICE64`（用户配置目录下的目录要登记进 RemoveFile 表）。
  处理：`ICE64` 用组件里的 `<RemoveFolder … On="uninstall" />` **真正修好**（它确实写入 RemoveFile 表，顺带让卸载清掉该子文件夹）；
  `ICE43`/`ICE57` 对 perMachine 包属误报（ICE 把 `DesktopFolder` 当每用户目录，而 `ALLUSERS=1` 时它就是 Public 桌面；
  ICE 建议的 HKCU KeyPath 会把每机器包写成每用户组件），所以在 `build-msi.ps1` 里用 `-sice:ICE43 -sice:ICE57`
  **精确抑制这两条**，其余 ICE 全部保留，并由真实安装/卸载回归兜底。
- `Package` 元素**没有** `Codepage` 属性，只有 `SummaryCodepage`；数据库代码页由 `.wxl` 的 `Codepage` 决定。
- DLL 型 CustomAction（`WixShellExec`）**不能**带 `Return="asyncNoWait"`（只有 `ExeCommand` 型才允许）。
- `WixUI_InstallDir` 自己定义了 `ARPNOMODIFY=1`，再写一遍会 `LGHT0091 Duplicate symbol`。
- 一个组件只能放**一个**文件才允许 `Guid="*"`（否则 `LGHT0367`）。
- `AllowSameVersionUpgrades="yes"` 会带来一条无害警告 `ICE61: The Maximum version is not less than the current product`。
- 命令行里**别用反引号续行**（会被拆坏成 `.7.0` 之类的假参数），用数组或一行写完。
- 回归脚本的**还原步骤必须先重建父目录**：`<RemoveFolder>` 让卸载把 `...\Start Menu\Programs\OptiScaler DLSSNR Installer\`
  整个删掉，而旧手动部署可能往同一目录放过同名快捷方式。直接 `Copy-Item` 回这个已不存在的目录会抛异常，
  连后面的“还原注册表值”“写结果文件”都不会执行（2026-09-26 那次回归就是这么失败的：退出码 1、结果文件没更新，
  看起来像测试没跑）。现在每项还原都 `try/catch`，且还原前 `New-Item -Directory -Force` 建好父目录。

## 真实安装/卸载回归

```powershell
powershell -ExecutionPolicy Bypass -File installer\test-msi-install.ps1
```

会自己弹 UAC 提权，流程：备份同名快捷方式与 `AppCompatFlags\Layers` 全部值 → `msiexec /i /qn` →
校验（哈希、快捷方式、`RUNASADMIN`、ARP 登记）→ `msiexec /x /qn` → 校验清理干净 → 还原备份；
结果写 `build\msi\msi-install-test-result.json`。

注意：本 MSI 是 perMachine，快捷方式只写“所有用户”的开始菜单与 Public 桌面；
老的手动部署（`D:\Program Files` 那份）留在**当前用户**开始菜单/桌面的同名快捷方式不属于它，
脚本只备份还原、不做断言，否则会误报残留。

另外两点实测行为（已写进 `MSI-安装说明.md`，也影响回归）：

- 本 MSI 会把同名快捷方式写进**同一个**「所有用户开始菜单」子目录，所以它会**覆盖**旧部署放在那里的一份；
  卸载时 `<RemoveFolder>` 连子文件夹一起删掉，被覆盖的旧快捷方式不会自己回来。回归前脚本会先把它备份、跑完再放回。
- 脚本退出码有含义：安装校验问题 + 卸载残留 + 还原失败，任何一项都会返回 **1**；全部通过返回 0。
