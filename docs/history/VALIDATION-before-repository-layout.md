# 2026-10-07 安装路径通用化验证

硬件版本 1.2.2、Bridge 1.1.1、游戏状态插件 1.1.0。保留原 string 构造入口，新增 BooBoopOptions 和 Bridge [Host] 配置；原厂路径改用系统默认目录或显式绝对路径，工作目录从 EXE 推导。协议、UUID 校验、动作映射与停止策略未改变。

- 五项新增路径测试及原有 42 项回归：**47/47 passed; 0 failed**。
- 三项目 Release 完整构建成功：**0 警告、0 错误**；命令与本机游戏引用同后文的验证记录。
- 源码检查与更新后的 SHA-256 文件清单：**87/87 通过**。
- 未运行游戏、原厂 EXE、蓝牙扫描或真实设备。
- 尚未选择或添加开源许可证，未建立远端或发布。

以下记录按时间保留，早期“源码未修改”“路径固定”及版本描述仅对应当时审查状态。

---

# 2026-10-07 本机代码审查与验证补充

以下是本次 Windows 本机验证结果；后文“v4 双路扩展验证记录”保留为原制作环境的历史记录，其未编译 / 未运行结论不代表本次状态。

| 检查 | 本次结果 |
| --- | --- |
| SDK / 命令行 runtime | SDK 10.0.302；.NET 6 runtime 6.0.33；x64 |
| 源码与包结构检查 | python scripts/verify_source.py：87/87 通过，文档修改后更新 manifest 并复核 |
| 既有 mock | dotnet run --project SelekaBooBoopBridge/tests/Bridge.MockTests/Bridge.MockTests.csproj -c Release：42/42 passed，0 failed |
| 三项目编译 | dotnet build BooBoopThreePart.sln -c Release -p:GameDir=目标游戏目录：0 警告、0 错误 |
| 引用路径 | D:\Games\Seleka\塞雷卡2 的原 core / interop |
| 游戏加载 / 观察 | 未执行 |
| 原厂 Native Host / 蓝牙 / 硬件 | 未执行 |

mock 构建出现 NETSDK1138 提示；目标仍为 net6.0。dotnet --info 的 workload 信息打印出现 InstallerBase 初始化异常，但后续实际构建与 mock 均成功，未安装或更换工具。上述命令未触发真实控制器、游戏或设备。

审查阅读了三个项目全部生产 C# 源码、项目引用、构建脚本和现有回归测试。三份项目 README 已重写，根 README 改为导航；控制源码不变。两项需进一步验证的来源边界为：活塞 UI 缓存缺少底层更新时间；面板唯一性只在恢复查找时检测，缓存有效期间新增面板不会立即被拒绝。物理执行 / 停止仍没有本次证据，历史道具 A/B 差异未解决。

---

# v4 双路扩展验证记录

日期：2026-10-07（UTC）。项目产品版本：游戏信号1.1.0，硬件1.2.1，组合1.1.0；程序集身份均保留1.0.0.0。此记录区分源码静态检查与未执行的运行验证。

## 已执行的检查

- `python scripts/verify_source.py`：87 项源码/项目/发布包不变量检查通过。覆盖三项目依赖方向、net6/x64、原引用集合、保留签名与GUID、双路映射、独立状态/停止/新鲜度、低频活塞缓存恢复、分通道硬件epoch、双停清理、单型号/动态UUID、mock生产源链接、manifest完整性。
- `bash -n build.sh` 通过；项目XML、solution路径、mock源路径与测试注册静态检查通过。
- 提供42项无游戏/无硬件mock，增加双输出稳定去重、单路Off/失效/超时、全停后新帧、交叉inflight状态合并、并发失败清理、每模式epoch隔离；仅检查源码，没有执行这些测试。
- 独立静态review检查了双路串行调度、pending stop不丢失、全停完成后新鲜边界、有效性与IL2CPP wrapper风险；移除了缺乏事实依据的“面板必须属于active scene”限制。
- `NativeHost.cs` 与原v3包逐字节一致。硬件控制器只把全局动作epoch替换为全局+每通道epoch记账；发现、FN010-RX白名单、动态UUID、命令编号0..9、协议格式、双停/最终清理路径保留。无厂商协议重写。
- 保留已有GameStateSnapshot构造器/成员、Observed心跳及程序集/命名空间身份；新增PistonStateHub独立API。游戏和桥接产品版本升1.1.0，硬件1.2.1；组合最低游戏依赖升到1.1.0。行为变化见COMPATIBILITY.md。
- 没有复制参考仓库源码；只使用固定提交中的精确活塞字段和0..3数字含义，来源和未确认边界见PISTON_SOURCE.md。
- SOURCE_MANIFEST.sha256覆盖除自身外的全部交付文件；ZIP内容逐项与源码比较、CRC核验。原v3源码ZIP保留，v4另存，不覆盖旧交付。

上述静态检查不是C#编译、并发运行、Unity/IL2CPP兼容或设备物理行为的证明。

## 实际尝试但被环境阻塞

`./build.sh --mock-only` 返回127：

```text
BLOCKED: dotnet SDK is not installed. No C# build or mock tests ran.
```

制作环境没有dotnet/csc/mcs/msbuild，也没有目标游戏core/interop引用。未安装额外工具，PowerShell构建脚本未在Windows实际运行。

因此本包未编译，42项mock均未运行，未生成DLL。不能将“42项测试源码齐备”描述为42项已通过。

## 未执行

- 未启动游戏、厂商EXE、真实控制器、蓝牙扫描或设备。
- 未验证目标安装的FindObjectsOfType/Il2CppType/IntPtr wrapper/反射字段访问能编译运行。
- 未确认UI缓存和真实活塞动作的延迟、生命周期及装备语义。
- 未验证两个BepInEx插件的加载、Harmony、跨程序集事件、游戏暂停/场景/卸载真实行为。
- 未验证物理震动/伸缩或物理停止，未重新调查此前道具A/B差异。
- 未创建Git提交、远端仓库、tag、remote或push。

历史FN010-RX协议实测不能替代本次改动验证。遥控器0..10与本包0..9协议编号未建立对应，4/6/8不承诺线性物理速度。

## 下一步

在有SDK与net6目标包的环境先运行：

```powershell
.\build.ps1 -MockOnly
.\build.ps1 -GameDir 'D:\Games\Seleka\塞雷卡2'
```

完整构建默认先mock，失败则阻止后续构建；脚本不会自动部署或启动游戏/设备。

随后可先只部署游戏信号DLL验证两路观察。决定进行设备验证时，退出游戏再部署三个配套DLL，移出旧插件和其他同设备控制源；设备离身放稳，保留物理停止/断电手段。分别记录软件写入与实际动作观察，检查双路并行、单路停止、全停和恢复新帧。任何写入成功都不是物理执行回执。
