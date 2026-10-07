# SecretFlasherManaka BooBoop Bridge 安装与使用

支持 Windows x64、既有 BepInEx 6.0.0-be.735 IL2CPP 游戏适配及 FN010-RX；不代表其他游戏版本或型号已经适配。

1. 退出游戏，将旧 BooBoopGameBridge.dll、SelekaBooBoopBridge.dll 及其他同设备控制插件移出整个 BepInEx/plugins 树。
2. 将 ZIP 中的 BepInEx 文件夹合并到游戏安装目录。三个 DLL 都放在 BepInEx/plugins/SecretFlasherManakaBooBoop/，不要保留同名副本或复制游戏依赖 DLL。
3. 安装 BooBoop 原厂软件并保留当前用户的 product_list.json 产品缓存。
4. 在 BepInEx/config/local.seleka.booboopbridge.cfg 中设置：

```ini
[Device]
PreferredAddress =

[Host]
ExecutablePath =
ProductCachePath =
```

地址空值要求只有一个合格 FN010-RX。原厂程序路径空值默认系统 Program Files 下的 Boo Boop/Boo Boop.exe，缓存空值默认 %APPDATA%/BooBoop/product_list.json。其他位置填写绝对文件路径，支持环境变量、中文和空格；无需引号。配置在加载时读取，修改后重启。

加载 Bridge 会自动尝试连接一次，先建立双输出停止基线，再跟随新观察帧。振动 Off/Low/High 映射 0/4/8；活塞 Off/Slow/Medium/Fast 映射伸缩 0/4/6/8。只观察游戏时仅安装游戏状态插件 DLL。

失败后不自动重连，检查 BepInEx/LogOutput.log 和配置后重启。卸载时退出游戏，再移出这三个 DLL；配置可保留。

活塞是 UI 缓存观察，不是运动反馈；管道写入不是动作或停止回执。运行设备时保留物理停止手段。

本安装包自有代码采用 MIT，Copyright (c) 2026 Lu_Noodles。许可证见 LICENSE，外部软件范围见 THIRD_PARTY_NOTICES.md。
