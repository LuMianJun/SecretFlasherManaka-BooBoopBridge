# Bridge.MockTests

不依赖第三方测试包的 .NET 6 console 回归测试。所有会话都只使用 `FakeController`，不会实例化真实 `BooBoopController`，不会启动厂商 EXE、连接硬件或加载 Unity/BepInEx。

测试直接链接生产环境的纯 CLR 状态、桥接与队列记账源码，覆盖震动和活塞伸缩的独立控制。`Program.cs` 保留入口、既有生命周期回归和 fake；`DualChannelTests.cs` 提供双通道、新状态 hub 和队列隔离回归。

## 运行

需要可构建 `net6.0` 的 .NET SDK 和运行该目标所需的 .NET 6 runtime，使用 x64 环境。在 `SecretFlasherManaka-BooBoopBridge` 仓库根目录执行：

```sh
dotnet run --project tests/Bridge.MockTests/Bridge.MockTests.csproj -c Release
```

退出码 0 表示全部通过，非 0 表示存在失败；每项测试打印独立 PASS/FAIL。程序启动时明确打印不会连接真实设备。

项目先查找仓库内子模块：

- `dependencies/SecretFlasherManaka-ForEveryThing/SecretFlasherManaka.ForEveryThing.csproj`
- `dependencies/BooBoopControl/BooBoopControl.csproj`

若对应路径不存在，则回退到当前交付的并列目录 `../SecretFlasherManaka-ForEveryThing`、`../BooBoopControl`。从 `tests/Bridge.MockTests` 计算，子模块路径为 `../../dependencies/...`，并列路径为 `../../../...`。

也可覆盖两个项目路径，建议使用绝对路径：

```sh
dotnet run --project tests/Bridge.MockTests/Bridge.MockTests.csproj -c Release -p:GameSignalsProject=/absolute/path/SecretFlasherManaka.ForEveryThing/SecretFlasherManaka.ForEveryThing.csproj -p:HardwareControlProject=/absolute/path/BooBoopControl/BooBoopControl.csproj
```

`GameSignalsProject` 只用来定位同目录的 `src/GameStateHub.cs` 和 `src/PistonStateHub.cs`，不会构建或引用游戏插件项目。项目直接链接这两个纯 CLR 文件、桥接的 `src/BooBoopGameSession.cs`、硬件项目的 `src/MotionEpochs.cs`，仅对 `BooBoopControl.csproj` 使用 `ProjectReference`，不需要 `GameDir`。链接 `MotionEpochs.cs` 是为了直接验证内部队列记账，而不是创建真实控制器。

## 输出与停止契约

- 震动：Off/Low/High → 0/4/8；活塞：Off/Slow/Medium/Fast → 0/4/6/8。
- Unknown、未识别枚举、inactive，以及源不可用/禁用的无效快照，只将对应输出设为零。
- 普通 Off 与该通道一秒没有新观察，调用对应的 `SetVibrationAsync(0)` 或 `SetStretchAsync(0)`。另一通道的新帧不能替代本通道的心跳。
- 单通道零值屏障不能被后续正值合并掉；同一通道随后收到的有效新值，可在零值完成后继续。
- 显式 `forceStop` 调用全局 `StopAsync`。两个通道都要各自收到在该次全局停止完成之后采集的新帧，才允许恢复；停止前和停止执行中的快照都不能重放。
- 初始化完成后先执行 baseline `StopAsync`。两个通道都不重放初始化期间收到的快照。
- 一个 worker 串行等待每次输出操作；完成后重新读取两个 mailbox。所有待处理的通道停止优先于新的正值。

## 覆盖范围

原有 42 项回归测试（新增 5 项路径测试见后文）：

1. 两套映射覆盖所有已知模式、Unknown、未识别枚举与 inactive；旧 `GameStateReason` 数值保持兼容，新原因追加。
2. 震动 hub 每次观察都触发 `Observed`；相同语义不重复触发 `StateChanged`；revision、timestamp 和强制停止标记正确。
3. 震动 hub 的坏订阅者和坏错误回调不阻断其他订阅者。
4. 初始化前、初始化中、baseline stop 中的两个通道快照都不可重放；重复 Start 只初始化一次。
5. 原震动 Low/High 映射、正向去重与 Off 单通道停止。
6. 震动来源的全局暂停屏障停止两个通道，并要求双方各自的停止完成后新帧。
7. inflight 震动期间 Off 后马上 High，仍先发送震动零值。
8. SourceDisabled 普通快照只停止它自己的通道，并验证独立恢复。
9. inflight 震动期间多个正向新值只保留最新值。
10. 一秒无震动新观察只发送震动零值，不重放。
11. 超过一秒持续收到相同 High，不把它误当成最大震动时长。
12. 初始化失败不自动重试，清理一次。
13. baseline stop 失败不允许正向输出，不自动重试。
14. 震动正向发送失败不自动重试，清理一次。
15. ready 变为 not ready 后清理，不自动重新初始化。
16. Dispose-before-Start 不初始化；所有 Dispose 等待同一次清理。
17. 初始化中 Dispose 取消初始化并清理一次。
18. 24 个并发 Dispose 与重复 Dispose 共享一次清理。
19. Dispose 后 Diagnostic/Error 订阅归零，不再转发事件。
20. 日志回调抛异常不阻止清理。
21. 活塞 hub 发布所有观察、语义去重、revision、timestamp、forceStop 和 Current。
22. 活塞 hub 的坏订阅者和坏错误回调相互隔离。
23. 双输出同时工作，稳定状态分别去重，改变一侧不重发另一侧。
24. 活塞 Slow/Medium/Fast/Off 依次实际调用 4/6/8/0。
25. 震动 Off 保留活塞；活塞 Off 保留震动。
26. 双方普通 Off 分别发送零值，重复 Off 去重。
27. 双方的 Unknown、无效枚举、inactive、SourceUnavailable 都只停止对应通道。
28. inflight 活塞期间 Off 后马上 Fast，仍保留伸缩零值屏障。
29. 两个通道的 Off 屏障都能在正值合并下保留，并在任何新正值之前执行。
30. 活塞来源的全局停止同样要求双方在停止完成后的新帧。
31. 挂起震动写入时收到活塞 Off 和震动新值，完成后先停活塞，再发送最新震动值。
32. 挂起活塞写入时收到震动 Off 和活塞新值，完成后先停震动，再发送最新活塞值。
33. 写入挂起期间两个通道的多个正值合并，不提前发送另一通道的旧值。
34. 活塞持续心跳不能让失去心跳的震动继续。
35. 震动持续心跳不能让失去心跳的活塞继续。
36. 两个通道各自持续观察超过一秒，都不重复发送或误停止。
37. ready 后才递交的旧时间戳也不可重放；每个通道必须有自己的新时间戳。
38. 活塞正向发送失败只尝试一次并清理。
39. 挂起活塞写入失败后，已排队的双通道新值不重放；并发 Dispose 共享一次清理。
40. 硬件队列记账：震动零值只使旧震动 ticket 失效，保留伸缩 ticket。
41. 硬件队列记账：伸缩零值只使旧伸缩 ticket 失效，保留震动 ticket。
42. 硬件队列记账：全局 stop 使两侧旧 ticket 失效，停止后的新 ticket 可用。

初始化、inflight 写入、全局停止和 cleanup 使用 `TaskCompletionSource` 闸门协调。fake 跟踪最大同时在途输出数，检查震动/伸缩/全局停止始终串行。条件轮询有 5 秒截止时间，单项测试另有 15 秒上限；少量固定时长窗口持续检查“不应发生”的事件或刷新观察，不用盲目 sleep 作为并发同步。全局停止测试以停止完成且 freshness cutoff 已更新后的诊断作为就绪信号，避免把 fake 的入口记录误当成完成。

## 验证边界

2026-10-07 Windows 本机补充验证：使用 SDK 10.0.302 和命令行 .NET 6 runtime 6.0.33，实际运行上述 dotnet run 命令，结果 **42/42 passed; 0 failed**。mock 构建提示 NETSDK1138，未改变 net6.0 目标。未启动游戏、厂商程序或真实控制器。原制作环境曾因缺少 SDK 未运行测试，该历史记录保留在组合包 ../../docs/VALIDATION.md；本次成功仅证明 fake 回归通过。

fake 记录的是桥接对硬件接口的调用；`MotionEpochs` 测试只验证队列失效记账。它们不验证厂商协议、实际传输队列集成、原生主机进程、实际输出停止、Unity 生命周期挂接或设备的物理行为。这些需要对应集成测试和实机验证。fake 的 `StopAsync`/`DisposeAsync` 记录接口边界，不模拟或证明双输出停止已在设备上完成。

## 安装路径通用化回归

2026-10-07 新增五项 BooBoopOptions 测试：系统与当前用户默认目录；非 C 盘 / 中文 / 空格及工作目录推导；环境变量展开；相对路径 / 非 EXE / 目录值拒绝；空白回退及路径规范化。只解析路径，不创建真实控制器或文件，不启动原厂程序。Windows 本机实际结果 **47/47 passed; 0 failed**（包含原有 42 项）。

本仓库提供 test.ps1 / test.sh 作为统一入口。47 项测试使用 fake，不创建真实控制器；生产 Bridge 命名空间现为 SecretFlasherManaka.BooBoopBridge。
