# 2026-10-07 本地仓库布局与命名验证

目录整理为 SecretFlasherManaka-ForEveryThing、BooBoopControl、SecretFlasherManaka-BooBoopBridge。游戏与硬件 CLR 身份保留；Bridge 改为 SecretFlasherManaka.BooBoopBridge 1.2.0，插件 GUID 和配置文件名保留。控制协议、游戏读取、动作映射、队列、停止和路径通用化实现保持原行为。

本次需验证：47 项 mock；三个项目 Release 编译；各仓库 build 入口；Bridge package ZIP 的三 DLL 白名单 / PE 头 / CRC；源代码检查及每仓库 manifest；相邻和 dependencies 两种布局；Git 元数据不影响 manifest。

验证环境沿用本机 SDK 10.0.302、命令行 .NET 6 runtime 6.0.33，以及原游戏 core / interop。net6.0 mock 构建存在 NETSDK1138 提示。

实际运行结果：

| 验证 | 结果 |
| --- | --- |
| 新 Bridge 的 package.ps1 完整流程 | 47/47 回归通过；构建、集中 DLL、生成 ZIP 成功 |
| 全量重新编译 | 0 错误；游戏源码出现 6 项既有可空引用警告（CS8602 / CS8604） |
| 两个独立仓库 build.ps1 | 构建成功；此处为增量编译 |
| 相邻布局 | 已用当前真实目录通过完整测试和打包 |
| dependencies 布局 | 在独立 scratch 副本验证自动定位；生产与 mock 工程均编译通过 |
| 源码不变量检查 | 83 项通过，Git / 包检查已拆分 |
| 独立仓库 manifest | 三个仓库分别生成并核验；排除 .git、dependencies、生成目录 |
| ZIP | 安装说明、MIT 许可证、第三方说明及三个 DLL；白名单、重复项、PE 头与 CRC 通过 |
| 负向验证 | manifest 篡改和错误 ZIP 被拒绝；Git 元数据及依赖不计入源码 manifest |
| 脚本 / 文档 | PowerShell 解析、Git Bash 语法和 Markdown 本地链接检查通过 |

可空引用警告来自 Unity 对象布尔有效性判断，未为了目录更名更改游戏读取逻辑。mock 构建的 NETSDK1138 也如前述保留。发布 ZIP 位于 Bridge 的 artifacts/releases，版本 1.2.0。随后按用户指示创建三个独立 Git 仓库与公开 GitHub 远端，添加 MIT（Lu_Noodles），由 Bridge 用子模块固定依赖。首次发布的结果见 GitHub 提交及 Release。前次原制作环境与路径通用化记录移至 docs/history/VALIDATION-before-repository-layout.md。


## GitHub 首次发布

游戏模块 v1.1.0：9bad6cd1f45d33bf5da30ff4d695e00bd94f104c。
硬件模块 v1.2.2：40447bc15c965c0921cd682a6a7fbd0886f84a7d。
Bridge 使用 .gitmodules 固定以上提交，并按此组合运行 47 项 mock、完整构建与 ZIP 校验。发布版本为 v1.2.0；仓库拥有者 LuMianJun，MIT 版权署名 Lu_Noodles。

完整安装包和支持范围由 Release 说明提供。
