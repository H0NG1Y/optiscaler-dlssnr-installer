# OptiScaler DLSSNR 安装助手 · MSI 安装说明

本文件说明 **MSI 安装包**（`OptiScaler-DLSSNR-Installer-<版本>-win-x64.msi`）会做什么、装到哪里、怎么卸载。
它和旁边的 `使用说明.md` 是互补关系：`使用说明.md` 讲助手界面怎么把组件装进游戏目录，本文件只讲 MSI 这一层。

## 一、MSI 会做什么

- 把助手程序与随附组件安装到 `C:\Program Files\OptiScaler DLSSNR Installer`；安装向导里可以改成别的目录（例如你之前手动部署的 `D:\Program Files\OptiScaler DLSSNR Installer`）。
- 创建**开始菜单（所有用户）**与**桌面（所有用户）**快捷方式「OptiScaler DLSSNR 安装助手」，指向安装目录中的主程序。
- 本 MSI 是 perMachine 安装，快捷方式写在“所有用户”位置（`C:\ProgramData\...\Start Menu\Programs\OptiScaler DLSSNR Installer` 与 `C:\Users\Public\Desktop`），任何账户都能看到。卸载时会删掉它创建的这一份桌面快捷方式，并把开始菜单里的 `OptiScaler DLSSNR Installer` 子文件夹整个移除。
- 正因为这个子文件夹归本 MSI 管：**旧手动部署放在同一位置的同名快捷方式会被覆盖**（安装时被本 MSI 那份替换），卸载后也不会自动回来。不受影响的是**当前用户**开始菜单/桌面里那份同名快捷方式（它们跟你账户绑定，只有你自己能删）。
- 如果你之前用 `deploy-v1.7.0.ps1` 手动部署过 `D:\Program Files\OptiScaler DLSSNR Installer`：建议装本 MSI 之前先删掉旧部署的快捷方式（或整个旧目录），否则桌面/开始菜单里可能出现两个同名图标 —— 一个指向 `C:\Program Files` 的新装版本，一个指向 D: 的旧版本。
- 给主程序写入「以管理员身份运行」兼容性标记（`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`）。助手要往游戏目录写文件，必须提权；卸载时会删掉**本 MSI 写入的这一项**，既不动 `Layers` 键下其他程序的标记，也不会删除这个共享的键本身。
- 在「设置 → 应用 → 已安装的应用」中登记，可正常卸载；安装更高版本 MSI 会按升级处理（同版本重复安装也会先移除旧版）。
- **不会**自动操作任何游戏目录。真正的游戏内安装/卸载仍然由助手界面完成，MSI 只负责把工具箱装进 Program Files。

安装、卸载都需要管理员权限（UAC）。下文命令里的 MSI 文件名请替换成你实际下载到的那个：本机整包形如 `OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi`，公开发行版形如 `OptiScaler-DLSSNR-Installer-Setup-v1.7.0.msi`。

## 二、包内清单

MSI 里装什么取决于构建参数（`build-msi.ps1` 的 `-NoModel` / `-NoFsr` / `-NoBridge`）。公开发行版只含助手本身，其余可选组件由使用者自备或由助手在线获取（见 `使用说明.md` 的「下载」章节）。

| 安装目录中的路径 | 公开发行版 | 本机整包 | 来源 | 说明 |
| --- | --- | --- | --- | --- |
| `OptiScaler-DLSSNR-Installer.exe` | ✓ 约 121 KB | ✓ | 本项目 `dist` | 助手主程序 |
| `使用说明.md` | ✓ | ✓ | 仓库 `README.md` 生成 | 助手界面使用说明 |
| `MSI-安装说明.md` | ✓ | ✓ | 本项目 `installer` | 本文件 |
| `SHA256SUMS.txt` | ✓ 约 0.7 KB | ✓ | 构建时生成 | 上述文件的 SHA-256 清单 |
| `nvngx.dll_dlssnr.dll` | ✗ | ✓ 114,688 字节 | 上游 v0.2.0 官方包 | 桥接 DLL |
| `nvngx_dlssnr.dll` | ✗ | ✓ 165,840,496 字节（约 158 MB） | 本机 DLSS-NR 310.8 素材 | 神经渲染模型，可选（见下节） |
| `FSR\dlss-enabler-headless.dll` | ✗ | ✓ 30,575,616 字节 | 本项目 `dist\FSR` | FSR 3 多帧生成 · Enabler |
| `FSR\dlssg_to_fsr3_amd_is_better.dll` | ✗ | ✓ 3,038,208 字节 | 本项目 `dist\FSR` | FSR 3 帧生成 · Nukem's |

**任何版本都不会打进 MSI 的内容**，以及原因：

- `streamline\`（6 个 DLL）：助手自带「刷新 SDK」，可从 NVIDIA-RTX/Streamline 官方 Release 下载并校验 SHA-256，属于"能在线获取"的部分，因此不随包分发。
- `RTXMFG\`：1.6.0 起已移除相关选项，不再随包分发；如果旧部署目录里还有这个文件夹，可以手工删除。
- 模型属于本机素材，请勿再次分发（见许可协议页第四节）。

## 三、安装

1. 双击 MSI，按向导操作：同意许可协议 → 选择安装目录 → 安装。
2. 完成页可以勾选「安装完成后运行 OptiScaler DLSSNR 安装助手」（默认不勾选；勾选会弹出 UAC 提权提示）。
3. 静默安装（管理员命令行）：

```powershell
msiexec /i "OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi" /qb
```

## 四、不安装 158 MB 模型（仅本机整包）

模型单独放在一个可选功能里，默认安装。不需要时用命令行排除：

```powershell
msiexec /i "OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi" REMOVE=NeuralRenderingModelFeature /qb
```

排除后助手仍可正常使用：你可以在界面里自己指定本机的 `nvngx_dlssnr.dll`。

## 五、校验

- 安装目录中的 `SHA256SUMS.txt` 列出了随包文件的 SHA-256。
- 不安装也能校验：管理安装会把文件按原目录结构释放到指定位置，且不写注册表、不建快捷方式。

```powershell
msiexec /a "OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi" /qb TARGETDIR="$env:TEMP\optiscaler-msi-check"
Get-ChildItem "$env:TEMP\optiscaler-msi-check" -Recurse -File | Get-FileHash -Algorithm SHA256
```

## 六、卸载

「设置 → 应用 → 已安装的应用」→「OptiScaler DLSSNR 安装助手」→ 卸载，或：

```powershell
msiexec /x "OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi" /qb
```

卸载会删除安装目录中**由本 MSI 安装的文件**、快捷方式与管理员兼容性标记，**不会**碰：

- 任何游戏目录中的东西（包括助手在游戏目录留下的备份目录 `.optiscaler-dlssnr-installer\`）；
- 你自己后来放进安装目录的文件；
- `streamline\`、`RTXMFG\` 等不是本 MSI 安装的目录。

## 七、排错

```powershell
msiexec /i "OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi" /l*v "$env:TEMP\optiscaler-msi.log"
```

- 提示需要 .NET Framework 4.8：先装 .NET Framework 4.8（Win10 1809+ / Win11 通常已内置）。
- 提示已安装更高版本：先卸载旧版本再安装。
- 安装被 360/杀软拦截：本包不含任何联网或注入行为，只是文件复制 + 两项注册表登记，可按需放行。
