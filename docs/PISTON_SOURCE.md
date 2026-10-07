# 活塞来源与实现边界

## 字段事实

参考仓库：`tzwgoo/SecretFlasherManaka-Link-YOKONEX`。固定提交：`3ff66ec1531c80b5fab82157a56bdb13e9a8b4f7`。

- 精确类型：`ExposureUnnoticed2.ObjectUI.InGame.VIbeStatePanel.VibeStatePanelView`，注意 `VIbe` 中大写 `VI`。
- 精确实例成员：`currentPistonMode`。
- [PolledGameValueReader.cs](https://github.com/tzwgoo/SecretFlasherManaka-Link-YOKONEX/blob/3ff66ec1531c80b5fab82157a56bdb13e9a8b4f7/src/ManakaLinkYokonex/Runtime/PolledGameValueReader.cs) 的 `ReadPistonMode` 返回 `int?`，直接返回面板该成员的 nullable 读取。参考没有证明游戏中存在某个活塞 enum 类型或 enum 成员；本实现只接纳 CLR Int32 字段/属性，不猜 enum。
- [CommandIdResolver.cs](https://github.com/tzwgoo/SecretFlasherManaka-Link-YOKONEX/blob/3ff66ec1531c80b5fab82157a56bdb13e9a8b4f7/src/ManakaLinkYokonex/Integration/CommandIdResolver.cs) 按整数解释 `0=off、1=slow、2=medium、3=fast`。这是参考集成的数字语义，不等于已用本地游戏 DLL 核验。
- 本包的 `PistonMode` 是独立公共 CLR 语义类型，不是对真实游戏 enum 元数据的声明；映射到伸缩 0/4/6/8 属于本次用户明确指定的桥接策略。

只使用以上字段身份与数值语义事实，独立编写适合本包的观察/事件/控制实现。没有复制参考仓库实现代码，也没有将无 LICENSE 的外部项目并入本包。

## 不作出的推断

没有确定实际活塞模式的写入方法，因此没有猜方法名或安装活塞写入 Hook。振动仍使用原 `CommonVibratorController` 当前 Leader 在 Update 后计算的实际 `VibrationStrength`，不换成 UI requested mode。

参考没有证明该面板的真实生命周期、归属场景、装备状态或更新时机。面板可能属于 additive 场景或 DontDestroyOnLoad，所以本包不要求它的 scene 等于 active scene。Unity 的 `isActiveAndEnabled` 只是本包的保守来源有效策略，不能证明已装备/正在运动。

面板缓存可能滞后，即使启用也可能保存旧值。每帧读到同样的有效值只能证明观察者仍在读，并不能证明底层生产者刚更新，也不能证明游戏执行器或实体设备动作。本包不以轮询时间戳掩盖这一区别。约1秒 watchdog 保护的是观察中断，不是底层缓存停更检测。

## 自己实现的有效性策略

反射成员/IL2CPP 类型/IntPtr wrapper 构造器在初始化时解析。精确 `currentPistonMode` 必须是 Int32 字段或无参数 getter 属性；不匹配时活塞通道不可用，不寻找近似字段或用默认 Off 代替。游戏主振动绑定失败仍按原实现全局失败关闭。

缓存有效时只做 Unity 对象存活、组件启用和字段读取/比较。没有每帧 Find。缓存缺失/禁用时最多每秒恢复查找一次，接纳唯一启用的准确类型对象；多个候选拒绝猜选。发现/重新启用后至少等下一观察帧再发布有效值；活跃场景变化、暂停、整个源组件恢复时清掉旧对象缓存并重新发现。

普通 Leader 不可用只使振动无效；面板不可用只使活塞无效。整体暂停/场景切换/插件禁用/关闭要求两路全停。活塞字段读取异常只关闭活塞来源并记录一次；主游戏观察异常继续使用原全局关闭语义。

失效输出明确是 `Active=false、Mode=Unknown` 和相应 Reason。数字0只有在有效字段读到0时才是正常 Off。

`StateChanged` 仅语义变化；`Observed` 是向后兼容的逐帧健康心跳，不等于每帧业务变化或每帧设备发送。两路独立 mailbox 只在档位/有效性/停止状态变化时驱动指令。

## IL2CPP 与待验证边界

`Il2CppType.From(System.Type)` 的形态可从 [Il2CppInterop 官方源码](https://github.com/BepInEx/Il2CppInterop/blob/master/Il2CppInterop.Runtime/Il2CppType.cs) 确认。本包对 Find 返回的基类 Unity wrapper 使用精确类型的缓存 IntPtr 构造器包装，再用缓存反射成员读取，避免把基类 CLR wrapper 直接传给派生类型成员。

早期制作环境缺少 SDK 和游戏引用；随后本机已完成编译，但仍未验证运行时对象查找、反射访问及 UI 生命周期，也没有启动游戏。应先构建并单独部署游戏信号 DLL，核对以下观察：

1. 面板存在、唯一启用、销毁与重新出现。
2. Off/Slow/Medium/Fast 的原始数值、切换延迟与 UI 关闭后的行为。
3. 暂停/恢复、additive UI、活跃场景切换与来源组件禁用。
4. 与真实游戏活塞状态的对应关系；如 UI 缓存不能可靠代表用户要跟随的动作，应先查找真实来源，再调整绑定，不绕过失效保护。

以上均未被本次源码静态检查或桥接 fake 测试验证。
