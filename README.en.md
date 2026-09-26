<div align="center">

# OptiScaler DLSSNR Installer

**A Chinese-language helper that installs, backs up, and restores OptiScaler DLSS Neural Rendering**

[![GitHub](https://img.shields.io/badge/GitHub-181717?logo=github&logoColor=white)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer)
[![Stars](https://img.shields.io/github/stars/H0NG1Y/optiscaler-dlssnr-installer?color=yellow&label=stars&logo=github)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/stargazers)
[![Downloads](https://img.shields.io/github/downloads/H0NG1Y/optiscaler-dlssnr-installer/total?color=orange&label=downloads)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/releases/latest)
[![Windows Download](https://img.shields.io/github/v/release/H0NG1Y/optiscaler-dlssnr-installer?color=brightgreen&label=Windows%20Download&logo=windows&logoColor=white)](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/releases/latest)
[![C#](https://img.shields.io/badge/C%23-.NET-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)

</div>

## Overview

A Chinese-language, portable Windows 10/11 x64 installer helper for [Dagherbou/OptiScaler_DLSSNR](https://github.com/Dagherbou/OptiScaler_DLSSNR) and [wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass](https://github.com/wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass). Just run `OptiScaler-DLSSNR-Installer.exe`: no Python, no PowerShell modules, and no separate .NET SDK download. It uses the system .NET Framework 4.8 and the built-in Windows HTTPS download stack; on trimmed systems where those components were removed, restore them first or supply a local official ZIP.

The helper does exactly three things: it writes the upstream components you select into the game directory, it backs up every file it touches, and it lets you **Uninstall and restore** at any time. It runs with normal privileges by default and does not modify the registry, services, drivers, or startup entries, and it never launches the game for you. Current version: **1.7.0**.

## Download

Download the latest version (1.7.0) from [GitHub Releases](https://github.com/H0NG1Y/optiscaler-dlssnr-installer/releases/latest).

- `OptiScaler-DLSSNR-Installer-Setup-v1.7.0.msi` is recommended. It installs to `C:\Program Files\OptiScaler DLSSNR Installer` (the wizard lets you change the directory), creates Start menu and desktop shortcuts, registers Windows uninstall information, and marks the main executable to **run as administrator** — the helper must elevate to write into game directories. Uninstall it normally from **Settings → Apps**.
- For portable use, download `OptiScaler-DLSSNR-Installer-v1.7.0.exe`: a single file that runs directly, writes no installation information, and creates no shortcuts.
- Each release also includes `checksums.txt` and `.sha256` files for SHA-256 verification.

The public packages do **not** contain the following files. You either supply them yourself, or the helper fetches them temporarily from the supported upstream GitHub repositories through **Online download / Refresh**:

| File | What it is | How to get it |
| --- | --- | --- |
| `nvngx_dlssnr.dll` | The DLSS-NR Neural Rendering model (160 MB class) | Local material and not part of public distribution; supply your own and select it in the interface |
| `nvngx.dll_dlssnr.dll` | Upstream bridge file | Comes with the upstream official package fetched by **Online download**; with a local ZIP, that ZIP must already contain it |
| `dlss-enabler-headless.dll`, `dlssg_to_fsr3_amd_is_better.dll` | Optional FSR frame generation components | Supply your own and point the interface at a local FSR directory |
| The six Streamline DLLs | Optional DLSS frame generation components | Use the built-in **Refresh SDK** to download from the official NVIDIA-RTX/Streamline release and verify SHA-256 |

In other words: whatever is available from official GitHub releases (the upstream OptiScaler package and the Streamline SDK) is downloaded and verified by the helper, while local material or files the upstream does not redistribute must come from you.

## Usage

1. Close the game first, then select the **actual game executable**. For some games pick the binary under `Binaries\Win64` rather than the launcher.
2. **Upstream repository** starts empty: choose **Dagherbou · DLSSNR** or **wilsjo2 · PreSR-Multipass**, then select **Check version**. Only GitHub release ZIPs from these two repositories are accepted as download sources, and an official SHA-256 is required. A local ZIP may be used instead and takes priority when filled in.
3. Select your local **`nvngx_dlssnr.dll`**. If a model with that name sits next to the helper, its path is filled in automatically; clear the field to reuse a model already present next to the game executable. Without a model, clear **Enable Neural Rendering** to install OptiScaler only.
4. **Extras and compatibility fixes**:
   - **Streamline compatibility fix**: always selectable. Use it only when the game fails to start, frame generation conflicts, or the game's local Streamline must be replaced; files go to the game executable's own folder and originals are backed up.
   - **FSR 3 · Enabler / Nukem's**: always available, regardless of the selected upstream. The DLLs must be provided locally.
5. Normally keep the `dxgi.dll` proxy. Per the official scripts, Vulkan games are worth trying with `winmm.dll`, and XGP / Microsoft Store games with `winmm.dll` or `version.dll`. `OptiScaler.asi` requires an ASI loader already present in the game.
6. Select your GPU and click **Install / Update**. Afterwards start the game yourself; current v0.2.0 opens the OptiScaler menu with **Insert (Ins)** by default so you can check Neural Rendering. Home was the older default hotkey; for older versions or custom hotkeys, read `[Menu] ShortcutKey` in the game directory's `OptiScaler.ini`.

The model is not included in this installer or in the upstream release package; `nvngx.dll_dlssnr.dll` is the upstream bridge file and cannot replace the model. The verified v0.2.0 documentation requires driver **616.56** or newer, and pre-Blackwell architectures need a compatible modified model. The helper only validates that a DLL is x64; it does not verify model version, origin, GPU compatibility, or image quality — refer to the [official release notes](https://github.com/Dagherbou/OptiScaler_DLSSNR/releases) of the version you install.

## Features

- Chinese WinForms interface with asynchronous operations, progress, and a log; preparation and downloads can be cancelled at any time
- Connects only to the supported upstream repositories — **Dagherbou · DLSSNR** and **wilsjo2 · PreSR-Multipass** — and verifies asset length plus the SHA-256 reported by GitHub; caches are re-verified before every use
- Accepts an already downloaded local official ZIP and checks its structure, paths, and required files (a local ZIP is **not** proof of official origin)
- Proxy choices: `dxgi.dll`, `winmm.dll`, `version.dll`, `dbghelp.dll`, `d3d12.dll`, `wininet.dll`, `winhttp.dll`, `OptiScaler.asi`; GPU choices: NVIDIA / AMD / Intel
- Keeps your existing INI settings and only touches the `[DlssNr] Enabled` value this run needs; for AMD/Intel it sets `[Spoofing] Dxgi` according to **Use DLSS inputs**, while NVIDIA keeps that entry as is
- Never installs the optional OptiPatcher and never executes upstream batch, registry, or Linux scripts
- Backs up files and records a manifest before committing, rolling back automatically on failure; **Recover interrupted operation** resumes after an abnormal exit
- One-click **Uninstall and restore** that only handles files in this tool's manifest instead of deleting a game folder by wildcard
- Optional FSR / DLSS frame generation components and Streamline SDK refresh, described below

### Optional FSR frame generation components

The FSR directory only needs the DLLs for the components you select, and the file names must match exactly. The helper copies them under their original names into the **`OptiScaler` subdirectory next to the game executable**; placeholder files such as "put this in the OptiScaler folder" need not be copied.

| Installer option | Local DLL file name | OptiScaler v0.2.0 menu entry |
| --- | --- | --- |
| FSR 3 Multi Frame Generation · Enabler | `dlss-enabler-headless.dll` | `Enabler` (FSR 3 MFG) |
| FSR 3 Frame Generation · Nukem's | `dlssg_to_fsr3_amd_is_better.dll` | `Nukem's` (FSR 3 FG) |

The two files can coexist; pick one of them in the OptiScaler menu. Selecting both during installation does not mean both run at the same time. The helper does not modify `FGInput`, `FGOutput`, `FGNvngxReplacement`, or the frame generation switch, so existing frame generation settings stay as they are. The default `[Libraries] OptiDllPath=auto` matches the `OptiScaler` subdirectory above; if you customised that path, confirm the actual load directory matches the file location.

For games that already provide usable DLSS frame generation input, press **Insert** to open the menu, set `FG Input` to `DLSSG via Nvngx` under `Frame Generation`, set `FG Nvngx` to `Enabler` or `Nukem's`, click `Save Settings`, restart the game, and enable DLSS frame generation in the game's own settings. Menu entries and support depend on the upstream version installed and on the game; a successful install does not guarantee the game supports the mode.

The optional DLLs come from you: the helper neither downloads these two components nor verifies their origin or actual frame generation effect. The file names and menu mapping above were checked against the upstream [v0.2.0 loader](https://github.com/Dagherbou/OptiScaler_DLSSNR/tree/v0.2.0-dlssnr/OptiScaler/framegen/nvngx) and [menu definitions](https://github.com/Dagherbou/OptiScaler_DLSSNR/blob/v0.2.0-dlssnr/OptiScaler/menu/menu_common.cpp#L3205).

### Optional DLSS frame generation components

Following community practice, **DLSS Frame Generation · Streamline** writes the Streamline components into the **game executable's own directory** and backs up any existing files of the same name. The helper ignores placeholder files in the source folder and processes only this whitelist.

**Six required DLLs** (the source directory must have all of them):

```text
sl.interposer.dll
sl.common.dll
nvngx_dlssg.dll
sl.dlss_g.dll
sl.reflex.dll
sl.pcl.dll
```

**Copied as well when present** (extra files common in community packages):

```text
nvngx_dlss.dll
sl.dlss.dll
sl.dlss_nr.dll
sl.nis.dll
nis.license.txt
nvngx_dlss.license.txt
reflex.license.txt
```

No other file from the source directory is copied, and no `OptiScaler\streamline` subdirectory is created. If an older version of this tool previously installed there, uninstall and restore first, then install to the game root with the current version.

Without a local Streamline directory, click **Refresh SDK** to list versions from the official **NVIDIA-RTX/Streamline** release; the helper downloads the ZIP, verifies its SHA-256, and extracts the six required DLLs. Afterwards you still need to choose frame generation input and output in the OptiScaler menu according to your GPU, driver, and game support, save the settings, and restart the game. A completed installation only means the components were written and backed up; whether frame generation actually works must be confirmed in game.

### Updates and restore

- Online downloads use only this project's official GitHub releases and verify asset length plus the SHA-256 reported by GitHub; caches are re-verified before every use. A local ZIP is checked for structure, paths, and required files but is **not** certified as an official source.
- Your existing INI settings are preserved and only the `[DlssNr] Enabled` value this run needs is changed; for AMD/Intel, `[Spoofing] Dxgi` follows the **Use DLSS inputs** setting, while NVIDIA keeps that entry. The optional OptiPatcher is not installed, and upstream batch, registry, or Linux scripts are never executed.
- Installation records every written file and its original backup. Updates keep using the baseline captured before the first installation; changing the proxy name requires uninstalling the old version first.
- Optional FSR / DLSSG components and their bundled licence files installed by this tool are preserved on later updates; clearing a checkbox means the component is neither added nor replaced this time — it is not uninstalled on its own, and files you placed manually outside this tool's management are never taken over. Re-selecting with a local component updates the files, while **Uninstall and restore** restores managed files from the pre-installation backups and removes files that did not exist before installation.
- Existing proxy files are never overwritten silently. If an OptiScaler not managed by this tool is detected, uninstall it the original way first. Compatibility with components such as ReShade must be verified per game.
- **Uninstall and restore** only touches files in this tool's manifest and never deletes a whole game folder by wildcard. A DLL replaced by another tool triggers a conflict and stops the entire operation.
- A modified `OptiScaler.ini` is saved to `.optiscaler-dlssnr-installer\saved-settings` before being restored on uninstall. Original backups stay in `originals` under the same directory.
- Preparation and downloads can be cancelled; the file commit phase either completes or rolls back. After an abnormal exit, use **Recover interrupted operation**. Do not run a game updater during installation, and do not edit the installation record by hand.
- Logs produced by running the game and plug-ins you add later are not tracked and are never deleted automatically. Once you no longer need the backups or saved settings after a successful uninstall, you may delete `.optiscaler-dlssnr-installer` yourself; keep it while you still manage the installation with this tool.

## FAQ

**The game directory is not writable and I get a permission error?**

If the game directory is protected by Windows permissions, close the tool and restart it with **Run as administrator**. The tool runs with normal privileges by default, does not modify the registry, services, drivers, or startup entries, and never launches the game. Follow the target game's mod rules.

**I do not have the `nvngx_dlssnr.dll` model — can I still use it?**

Yes. Clear **Enable Neural Rendering** and the helper installs OptiScaler only; reinstall or update once you have the model. The model ships with neither the installer nor the upstream release package, so you must supply it.

**Which upstream repository should I choose?**

It starts empty and you must pick one: **Dagherbou · DLSSNR** provides the DLSSNR route, while **wilsjo2 · PreSR-Multipass** is the alternative upstream. Only GitHub release ZIPs from these two repositories are accepted as online sources, and both require an official SHA-256.

**Which key opens the OptiScaler menu in game?**

Current v0.2.0 defaults to **Insert (Ins)**; Home was the older default hotkey. For older versions or custom hotkeys, read `[Menu] ShortcutKey` in the game directory's `OptiScaler.ini`.

**Which proxy file should I pick?**

Normally keep `dxgi.dll`. Per the official scripts, try `winmm.dll` for Vulkan games and `winmm.dll` or `version.dll` for XGP / Microsoft Store games; `OptiScaler.asi` requires an ASI loader already in the game. Uninstall the old version before changing the proxy name.

**Will uninstalling delete other files from my game?**

No. **Uninstall and restore** only handles files in the installation manifest, and a DLL replaced by another tool triggers a conflict that stops the operation. Logs produced by running the game and plug-ins added later are not tracked and are never deleted automatically.

**How do I verify an installer manually?**

Run `Get-FileHash .\OptiScaler-DLSSNR-Installer-Setup-v1.7.0.msi -Algorithm SHA256` in PowerShell (use the matching EXE file name for the portable build) and compare it with the `.sha256` file or `checksums.txt` from the same release.

## Source layout

- `src\`: C# sources — `Program.cs`, `MainForm.cs`, `ModernControls.cs`, `InstallerEngine.cs`, `PackageService.cs`, `Contracts.cs`, `CurlTransport.cs`, `AssemblyInfo.cs` (version 1.7.0)
- `installer\`: WiX v3 MSI definition and documentation — `Installer.wxs`, `WixUI_zh-CN.wxl`, `MSI-安装说明.md`, `inspect-msi-tables.ps1`, `test-msi-install.ps1`, `README.md`
- `build.ps1`: compiles the main program to `dist\OptiScaler-DLSSNR-Installer.exe`; `-Test` also compiles and runs three test suites
- `build-msi.ps1`: builds the MSI with WiX 3.14
- `tests\`: test sources
- `assets\`: icons and other resources; `app.manifest`: the application manifest
- `README.md`: this document's Chinese original, also shipped in the package as `使用说明.md`

The sources are split into an independent file engine, a GitHub download service, and the WinForms interface, with no third-party NuGet dependencies. Tests use temporary directories and never touch your real game installation.

```powershell
.\build.ps1
.\build.ps1 -Test
# Optional: test a temporary game directory with the already downloaded official v0.2.0 full package
.\build.ps1 -Test -OfficialZip 'path\to\full.zip'
```

## Disclaimer

- This is an independently made installation helper and **not** an official upstream installer; it is not affiliated with OptiScaler or the authors of the upstream repositories. The released EXE bundles no upstream DLLs, and the upstream packages it downloads and their licences remain the property of their respective authors.
- The helper only validates that a DLL is x64; it does not verify model version, origin, GPU compatibility, or image quality. Refer to the official release notes of the version you install for support information.
- The DLSS-NR model is local material that upstream explicitly does not redistribute — do not redistribute it, and the public packages do not include it either.
- Modifying a game directory with this tool is your own choice; follow the target game's mod rules, and you alone are responsible for any consequences of using this tool.

[中文说明](README.md)
