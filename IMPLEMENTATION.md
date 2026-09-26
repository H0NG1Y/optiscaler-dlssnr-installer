# OptiScaler DLSSNR 安装器

目标：Windows 10/11 中文便携 EXE，选择实际游戏 EXE、官方 Release 和本机模型 DLL，完成安装、更新、备份和卸载还原。

实现使用系统 .NET Framework 4.8 / WinForms 和内置 ZIP、HTTP、JSON 库，不引入第三方运行库。

- `Contracts.cs`：界面、下载服务、文件引擎的数据接口。
- `PackageService.cs`：仅连接官方 GitHub API 和 Release，列出完整 ZIP 包、下载和 SHA-256 校验，支持已下载 ZIP。
- `InstallerEngine.cs`：严格限定目标目录，识别 64 位 PE，安全解压，代理 DLL 冲突检测、INI 配置、备份清单、事务回滚和卸载恢复。保留用户修改过的文件，提示解决冲突后重试。
- `MainForm.cs` / `Program.cs`：中文界面、异步操作、进度、日志和错误提示。
- `Tests.cs`：真实临时目录验证备份还原、更新后基线、修改冲突、ZIP 路径越界、损坏备份、取消和失败恢复。

不执行压缩包中的批处理/注册表文件；不安装驱动或修改系统设置。模型 DLL 由用户提供。所有真实游戏内兼容性仍须由目标游戏运行确认。

实施顺序：先运行文件引擎失败用例；并行实现下载服务与界面；补齐引擎并通过回归；使用真实官方发布包做临时目录安装/卸载测试；编译发布 EXE、编写使用说明并记录验证结果。
