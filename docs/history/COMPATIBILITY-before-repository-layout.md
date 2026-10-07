# 版本、兼容性与验证边界

## 1. 初始三项目组合

| 项目 | 独立产品版本 | AssemblyVersion | 插件 GUID / 性质 |
| --- | --- | --- | --- |
| SecretFlasherManaka.ForEveryThing | 1.1.0 | 1.0.0.0 | `local.seleka.gamesignals` |
| BooBoopControl | 1.2.1 | 1.0.0.0 | 普通类库，无 BepInEx 插件 GUID |
| SelekaBooBoopBridge | 1.1.0 | 1.0.0.0 | `local.seleka.booboopbridge` |

这些版本独立演进，不要求三个数字始终一致。2026-10-07 本机已生成三个 Release DLL 并通过 42 项 mock；未创建 Git tag 或远端 release，也未验证游戏加载或真实设备。

三个程序集名称固定为 `SecretFlasherManaka.ForEveryThing`、`BooBoopControl` 和 `SelekaBooBoopBridge`；命名空间分别为 `SecretFlasherManaka.ForEveryThing`、`BooBoopBridge` 和 `SelekaBooBoopBridge`。新游戏接口不在旧 `BooBoopGameBridge` 程序集/命名空间中。

游戏插件的显示名为 `SecretFlasherManaka For EveryThing`。之前更名保留插件 GUID `local.seleka.gamesignals` 作为稳定技术 ID，不改变游戏绑定、设备命令或支持范围，也不更名其他两个项目。但程序集与命名空间更名改变类型标识：更名前编译的消费者必须更新引用并重新编译，保留 GUID、版本号和 API 成员不构成二进制兼容保证。部署时先移出重命名前的游戏信号 DLL，避免同一 GUID 的两个版本同时加载。

## 2. 依赖契约

### 游戏插件

`SecretFlasherManaka.ForEveryThing` 是独立 BepInEx 6 IL2CPP 插件。其公共契约是纯 CLR 的 `GameStrength`、`GameStateSnapshot` 和 `GameStateHub`，不暴露 Unity 对象，不引入硬件档位。

兼容性不止是方法名。以下语义也属于契约：

- `Off`、`Low`、`High`、`Unknown` 的含义和枚举值。
- `Active`、生命周期 `Reason`、`Revision` 和 `ObservedAt` 的含义。
- `ObservedAt` 是进程内 `Stopwatch` 计数，不是壁钟时间。
- `StateChanged` 仅在语义变化时触发；`Observed` 可持续刷新观察，`forceStop` 表示不可被随后动作覆盖的停止屏障。
- 事件在 Unity 主线程观察/生命周期路径同步调用；消费者不得阻塞。
- 发布者停止工作时的失效/关闭语义：来源组件禁用立即发布停止，重新启用后当帧失效，等待新观察。
- `GameStateHub.ApiMajorVersion == 1` 这一代契约的运行时标记。

删除或重命名类型、改枚举值、改快照构造/字段、改事件签名或改变线程/停止语义，都可能破坏现有消费者。只让代码重新编译通过不足以证明这些语义仍兼容。

### 硬件类库

`BooBoopControl` 不依赖游戏。`BooBoopController` 与 `IBooBoopController` 的初始化、伸缩/震动 `0..9`、停止和异步释放是组合插件的控制契约。接口允许注入 fake，但不是对所有硬件都适用的通用控制层。

保持以下约束：就绪前拒绝动作；单实例初始化不自动重试；控制串行发送；全局停止使两路更早的未开始请求失效，单路零值只使本路失效；停止尽力尝试两个输出；断连/传输故障后不重连、不重放；只清理自己创建的主机进程。

新增接口成员也可能破坏第三方实现的 fake 或适配类；不要一概当成“只增加功能所以兼容”。更改 0..9 意义、设备选择、动态 UUID 来源、白名单、停止行为或事件线程，都需要专项评估与测试。

### 组合插件

`SelekaBooBoopBridge` 用项目引用依赖前两项，在运行时对 `local.seleka.gamesignals` 声明版本范围 `>=1.1.0 <2.0.0` 的硬依赖，并在初始化时检查 `GameStateHub.ApiMajorVersion`、`PistonStateHub.ApiMajorVersion` 与 `BooBoopController.ApiMajorVersion` 均为 1。在此加载器中不要把裸 `"1.0.0"` 当作最低版本范围；这里明确写出 1.x 范围。这些保护**不能代替范围内每次更新的完整 API 与语义兼容检查**。硬件类库不是插件，其依赖通过程序集解析满足，不会因没有插件 GUID 而自动变得可选。

桥接层自己拥有振动 Off/Low/High → 0/4/8，活塞 Off/Slow/Medium/Fast → 伸缩0/4/6/8 的规则。它不改变游戏枚举来容纳硬件级别，不让硬件类库直接引用游戏快照。映射策略变更只应落在组合项目，并单独发布说明。

## 3. AssemblyVersion 与独立发布

本次三个项目的 `AssemblyVersion` 均为 `1.0.0.0`，用于这一代契约的程序集标识。独立产品版本用于区分各项目的发布内容。

固定 AssemblyVersion **不是**“可以任意混装 DLL”的保证。类型签名、事件语义、依赖版本和加载上下文都可能导致失败。对于兼容更新，应保持公共契约，更新相应项目的产品版本，并重新验证组合。

对于破坏性变更，应明确提升该项目的主版本、重新评估程序集版本策略，并更新/重编译相关消费者及依赖声明。不要靠保留 `1.0.0.0` 掩盖 API 破坏，也不要只放宽 BepInEx 依赖范围或修改 API 主版本标记就声称解决了兼容性。

推荐每次发布记录：

- 三个项目各自的产品版本与真实 commit SHA。
- 构建使用的 SDK、目标框架、游戏/加载器/Unity 版本。
- mock、编译、插件加载、游戏观察、设备实测各自的结果。
- 是否改变公共 API、映射、协议、停止/清理或日志行为。
- 可回退到的上一组 DLL/源码提交。

Git submodule 固定的是具体 commit，独立目录相邻摆放不提供这种版本锁定。配置方法见 [GIT_SUBMODULES.md](../GIT_SUBMODULES.md)。

## 4. 如何判断可以独立更新

### 只更新游戏插件

可在不改硬件代码的情况下修复游戏绑定或诊断，但需要确认公共游戏契约兼容，并重跑组合 mock、两个插件构建与实际游戏观察验证。游戏更新后，即使程序集仍加载成功，也必须重新确认精确 Leader、实际强度与生命周期路径。

不要把 A/B 道具差异当成已解决。当前仍只观察准确的 `CommonVibratorController` Leader，尚未调查为什么道具 B 未产生预期输出。

### 只更新硬件类库

不必修改游戏插件。需确认 `IBooBoopController`/`BooBoopController` API 和行为兼容，重跑组合 mock 与构建。若涉及设备选择、UUID、协议或清理，要做相应的受控设备验证；fake 无法验证真实主机协议。

目前只支持 FN010-RX。支持新型号需要该型号的明确协议实现与验证，不能通过放宽名称白名单、接受未知 service_data 或硬套既有 UUID 来声称支持。

### 将来增加硬件能力的边界

同样具有当前伸缩/震动档位能力的其他型号，可以在验证它自己的发现、连接、协议与停止行为后，复用现有控制接口的语义；型号不同不一定要求修改游戏插件。但复用接口不代表能复用 FN010-RX 的命令表、UUID 或设备选择逻辑。

如果新硬件提供位置控制、行程、持续时间等不同能力，应增加语义明确的能力接口与对应组合策略，不把位置或毫秒值硬塞进现有 0..9 档位。硬件支持哪些能力必须明确可识别；不支持时明确拒绝，不能静默用震动代替位置、用伸缩代替其他动作或忽略安全停止要求。

这只是后续扩展边界，不是当前已实现的泛用设备框架。本次仍只有 FN010-RX 实现，未知型号保持拒绝；新增型号/能力需要单独设计、实现、测试和授权运行验证。

### 只更新组合插件

保持前两项契约不变时可独立调整映射或队列逻辑。需重跑 mock、组合构建，并验证初始化完成后的双输出停止基线、新鲜度、停止屏障、断连不重试、暂停/场景/卸载清理，以及组合组件禁用后永久结束会话的行为。改变输出模式或档位时必须清楚说明，而不是静默复用旧版说明。

### 实际部署规则

- 首次迁移必须整体部署本次三个配套 DLL，并移出旧整体插件。
- 后续兼容的单项更新可以单独替换对应 DLL，但必须先退出游戏，确认它是已验证组合的一部分，且 plugins 中没有其他同名副本。
- API/语义破坏或组合版本不确定时，重新构建并整体部署三项，不能只换其中一个试运气。
- 回退以已记录的配套版本为单位，必要时恢复相应配置。升级或回退前保存现有文件；不边运行边覆盖插件。

## 5. 从旧整体插件迁移

旧 `BooBoopGameBridge.dll` 把游戏与硬件代码编入一个程序集，使用 `local.booboop.unifiedstretch`。新方案分成两个 BepInEx 插件加一个硬件类库，程序集名、游戏命名空间、插件 GUID 和配置文件均发生变化。

这不是旧程序集的二进制替身。原先引用 `BooBoopGameBridge.dll` 或旧 `GameStateHub` 的第三方 Mod 需要改为引用 `SecretFlasherManaka.ForEveryThing.dll`，更新命名空间和 BepInEx 依赖声明后重新编译。不要同时保留旧整体插件来“兼容”旧 Mod，因为它仍可能自行输出到同一设备。

新配置：`BepInEx/config/local.seleka.booboopbridge.cfg`，`[Device] PreferredAddress`。根据需要手动迁移已确认的地址；不要假设旧配置自动迁移。

本次组合正向动作是独立震动 4/8 与活塞伸缩 4/6/8。单路 Off/无效只停该路；全局生命周期和最终清理双停。遥控器档位与此协议编号不存在已验证的一一/线性对应。

## 6. 固定环境、可验证内容与不能保证的内容

目标保持 Windows x64、BepInEx `6.0.0-be.735`、IL2CPP、.NET `6.0.7`、Unity `2022.3.62f2`。项目使用 net6.0，不要求升级游戏运行时。构建时引用该游戏的原 core/interop，并检查文件路径；文件存在本身仍不证明 interop 可 patch。

当前证据分层（2026-10-07 本机补充）：

| 项目 | 已有证据 | 尚不能据此证明 |
| --- | --- | --- |
| 历史 FN010-RX 协议 | 用户此前实测连接、动态 UUID、伸缩 / 振动与停止 | 本组三项目的真实运行与物理动作 |
| 当前三项目 | Release 构建成功，0 警告、0 错误；87 项源码检查通过 | 游戏实际加载、Harmony 和 IL2CPP 观察正确 |
| mock 工程 | 42/42 fake 回归通过 | 原厂协议、UI 生命周期或物理停止 |
| 本机验证环境 | SDK 10.0.302、命令行 .NET 6.0.33；未启动游戏 / 主机 / 设备 | 真实链路验证完成 |
| A/B 道具差异 | A 可动、B 未动是历史反馈 | 原因已定位或问题已修复 |

实际命令和原制作环境历史记录见 [验证记录](VALIDATION-before-repository-layout.md)。

建议按以下顺序补齐验证，并分别保留日志：

1. 在有 SDK 的机器运行纯 mock。失败时先修复，不能把测试删除或跳过后写“通过”。
2. 使用当前原游戏引用构建三个项目，核对输出名称、版本、引用路径和硬依赖。
3. 先单独部署游戏状态插件观察 Hook、Leader、快照和生命周期日志。这一步不需要硬件类库或组合插件。
4. 再在明确决定做设备测试时部署组合，确保没有其他控制源，设备离身放稳，保留物理停止手段。检查振动 Off/Low/High、活塞 Off/Slow/Medium/Fast、双路同时动作/单路停止、暂停、场景变化、观察中断和正常退出。
5. 单独记录命令写入与物理观察结果。不要为了制造故障强杀设备或游戏来假设停止必然有效。

任务返回、Connected 或“已写入原生主机”只证明软件处理到相应阶段。已在途字节不可撤回；停止不是硬实时承诺。强杀、崩溃、断电、系统进程卡住或链路断开时，无法保证软件清理或物理停止。

## 7. v4 双路扩展的兼容说明

保留三个程序集名称、命名空间、AssemblyVersion=1.0.0.0、插件 GUID、原 GameStateSnapshot 构造器和所有已有公开方法/事件签名。追加 PistonMode/PistonStateSnapshot/PistonStateHub、ApplyPistonSnapshot 和 MapStretch，不扩充 IBooBoopController 接口。原 Observed 逐帧健康心跳保留，StateChanged 仍只变化时发。

组合插件需要新增活塞 API，因此最低游戏插件产品版本提高到 1.1.0；旧 1.0.0 不满足此次组合，必须配套构建部署。硬件产品更新到 1.2.1，修正单模式0取消另一模式排队请求；FN010-RX 协议、0..9 范围、UUID、NativeHost 未修改。

行为变化明确：失效游戏快照显示 Unknown/Active=false 而非 Off；追加 SourceUnavailable/InvalidValue Reason 不改变旧枚举数字。旧第三方消费者必须检查 Active，不能把无效值当正常档位。桥接普通单路失效从旧版双停变为该路停止；全局双停完成后不再接受停止期间的旧正向缓存，必须接收新帧。新版启用伸缩正向输出是已授权新功能，不应当作纯重命名升级。

## 安装路径通用化更新

硬件产品版本为 1.2.2，Bridge 为 1.1.1，游戏插件保持 1.1.0；AssemblyVersion 均保持 1.0.0.0。BooBoopController 原有 string 构造签名保留，新增 (string, BooBoopOptions) 重载，IBooBoopController 不变。新增 [Host] ExecutablePath / ProductCachePath 配置，空值使用系统默认目录；协议 Origin、设备型号、UUID 校验与停止逻辑不变。旧缓存配置无需迁移，现有标准安装可使用空值。
