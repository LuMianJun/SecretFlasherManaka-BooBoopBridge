# 首次公开发布：1.2.0

完整安装包已通过编译和 47 项 mock。当前适配 SecretFlasherManaka 的既有 BepInEx IL2CPP 环境，以及 FN010-RX。

## 下载与安装

下载附件 `SecretFlasherManaka-BooBoopBridge-1.2.0-win-x64.zip`。退出游戏，移出旧 `BooBoopGameBridge.dll`、`SelekaBooBoopBridge.dll` 和其他同设备控制插件，再把 ZIP 内的 BepInEx 目录合并到游戏安装目录。

包内提供三个配套 DLL、INSTALL.md、MIT LICENSE 和 THIRD_PARTY_NOTICES.md。详细配置和日志排查见 [总项目 README](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge#readme)。

`BepInEx/config/local.seleka.booboopbridge.cfg` 支持 `[Device] PreferredAddress` 和 `[Host] ExecutablePath / ProductCachePath`。原厂安装路径可自定义，空值使用系统默认目录。加载 Bridge 会自动尝试连接一次，并在建立双停基线后跟随新的游戏帧。

## 功能与配套版本

- 游戏振动 Off / Low / High 映射振动协议编号 0 / 4 / 8。
- 活塞 Off / Slow / Medium / Fast 映射伸缩协议编号 0 / 4 / 6 / 8。
- 双通道独立去重、停止屏障、观察超时和异步清理；失败不自动重连。
- ForEveryThing **1.1.0**，提交 `9bad6cd1f45d33bf5da30ff4d695e00bd94f104c`。
- BooBoopControl **1.2.2**，提交 `40447bc15c965c0921cd682a6a7fbd0886f84a7d`。
- Bridge **1.2.0**，通过 Git 子模块固定以上依赖。

## 验证与边界

47/47 mock、83 项源码检查、Release 编译及 ZIP 白名单 / PE 头 / CRC 检查通过。全量游戏源码编译存在 6 项已有可空引用警告，mock 构建提示 net6.0 支持状态。

活塞读取 UI 缓存，持续心跳不证明游戏执行器正在运动。多面板唯一性只在查找时校验；历史道具 A/B 差异未解决。管道写入不证明物理执行或停止，运行设备时保留物理停止手段。包内不包含原厂 EXE、游戏 DLL 或个人缓存。

三个仓库自有代码采用 **MIT**，Copyright (c) 2026 **Lu_Noodles**；游戏、原厂软件和第三方依赖不属于该授权范围。

## SHA-256

`0b3441262f9777a4c14cc88eb1dd0b4945d97b59cb8d4152c3e30b811d7fc56b`
