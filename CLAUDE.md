# CLAUDE.md

## DLL 引用策略

**MyRobotSDK 已冻结，其源码已从解决方案中移除，仅保留编译产物作为二进制依赖。**

- `SAMCS_WPF/lib/MyRobotSDK.dll` — 托管程序集 (`.csproj` 中以 `<Reference>` + `<HintPath>` 引用)
- `SAMCS_WPF/lib/MyRobotSDK.xml` — IntelliSense 文档，MSBuild 自动随 DLL 复制到输出
- `SAMCS_WPF/lib/MyRobotSDK.pdb` — 调试符号，允许调试时步进 SDK 源码
- `SAMCS_WPF/lib/x64/ftcoreimc.dll` — 原厂非托管驱动 (`<Content>` + `CopyToOutputDirectory=Always`)
- MyRobotSDK 源码保留在仓库中供查阅，但不参与解决方案编译

## 编码风格

- C# 命名：类/方法 `PascalCase`，私有字段 `_camelCase`
- XML 文档注释：所有 public API 均附带 `<summary>` 和参数说明
- `internal` 隔离：HAL 层全部 internal，SDK 对外仅暴露 Controllers + Models + Exceptions
- 项目文件使用 `.csproj` + `.slnx`（新格式解决方案文件）