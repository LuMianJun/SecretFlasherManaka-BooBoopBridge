# 当前版本与兼容契约

| 仓库 | 程序集 / C# 项目 | 产品版本 | 公共命名空间 |
| --- | --- | --- | --- |
| SecretFlasherManaka-ForEveryThing | SecretFlasherManaka.ForEveryThing | 1.1.0 | SecretFlasherManaka.ForEveryThing |
| BooBoopControl | BooBoopControl | 1.2.2 | BooBoopBridge |
| SecretFlasherManaka-BooBoopBridge | SecretFlasherManaka.BooBoopBridge | 1.2.0 | SecretFlasherManaka.BooBoopBridge |

三项 AssemblyVersion 均为 1.0.0.0，不代表可以任意混装 DLL。游戏与硬件公开类型身份保留；Bridge 更名改变了程序集与命名空间，其直接消费者必须更新引用并重新编译。部署时移出旧 SelekaBooBoopBridge.dll，不要同时加载同 GUID 的两个版本。

插件 GUID 仍为游戏 local.seleka.gamesignals、Bridge local.seleka.booboopbridge；这些稳定技术 ID 不要求与公开游戏名相同。配置路径继续使用 local.seleka.booboopbridge.cfg。Bridge 要求游戏插件 >=1.1.0 <2.0.0，并检查两个 Hub 和硬件的 ApiMajorVersion 均为 1。

游戏接口兼容契约包括枚举数字、快照构造及成员、Active / Reason / Revision 的含义、Stopwatch 时间戳、主线程同步事件、变化事件与观察心跳的区别、forceStop 停止屏障。硬件契约包括 0..9 编号、初始化一次、不重连、任务异常、串行输出、分通道及全局取消、双停和异步清理。签名保留不等于行为可以随意改变。

新增硬件型号通常在 BooBoopControl 内维护型号独立的发现 / 协议 / 停止实现，并在其 README 中记录支持能力和实测状态。现有接口足够且语义不变时，游戏插件与 Bridge 可继续复用；新增位置 / 行程等能力需扩展明确的能力接口和桥接策略。不同硬件软件生态可以建立新控制库与新 Bridge。当前只有 FN010-RX，不因仓库重组扩大支持范围。

发布时记录三个产品版本、真实依赖提交、构建环境与各层验证结果。普通玩家从 Bridge 获取完整配套 ZIP；独立模块作者引用需要的仓库。三个仓库采用 MIT，版权署名 Lu_Noodles；外部软件和第三方依赖不在该授权范围内。

旧兼容说明保存为 docs/history/COMPATIBILITY-before-repository-layout.md，仅作为历史记录。
