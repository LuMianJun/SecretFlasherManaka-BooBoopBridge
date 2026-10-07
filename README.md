# SecretFlasherManaka-BooBoopBridge

这是 **SecretFlasherManaka 游戏到 BooBoop 硬件的桥接总项目**。它依赖独立的 **SecretFlasherManaka For EveryThing** 游戏信号插件与 BooBoopControl 硬件库，提供状态映射、设备会话、回归测试和三 DLL 完整安装包。

仓库名使用横线；C# 项目、程序集及命名空间为 `SecretFlasherManaka.BooBoopBridge`。Bridge 版本 **1.2.0**，游戏依赖 **1.1.0**，硬件依赖 **1.2.2**。插件 GUID 保留 `local.seleka.booboopbridge`，配置文件名也保留，避免已有配置因更名而丢失。

## 当前支持与能力

目标是 Windows x64、BepInEx 6.0.0-be.735 IL2CPP、既有 Unity 2022.3.62f2 游戏适配；目标框架 net6.0 / C# 10，既有游戏 runtime 为 .NET 6.0.7。BooBoopControl 当前仅支持 **FN010-RX**，没有全型号或跨游戏保证。

| 游戏通道 | 游戏模式 | 硬件输出 |
| --- | --- | --- |
| 振动 | Off / Low / High | 振动协议编号 0 / 4 / 8 |
| 活塞 | Off / Slow / Medium / Fast | 伸缩协议编号 0 / 4 / 6 / 8 |

两路独立去重、合并最新值、检测观察超时和保留单路停止屏障。全局停止完成后，每路必须收到新的观察帧才能恢复。一个 worker 串行发送，两路可同时保持输出。没有自动重连、菜单、HTTP 服务或多设备管理；协议编号不表示线性物理速度或行程。

## 普通用户：部署和使用

完整安装包由 [GitHub Releases](https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge/releases) 提供；普通用户下载 ZIP，无需分别下载源码仓库。开发者可递归克隆本仓库及依赖。

1. 安装上述兼容的游戏加载器及 BooBoop 原厂软件。保留原厂生成的产品缓存，不用其他游戏的 interop 替代。
2. 退出游戏，备份当前插件与配置。将旧 `BooBoopGameBridge.dll`、`SelekaBooBoopBridge.dll` 和其他同设备输出插件移出整个 `BepInEx/plugins` 树；不能留在 plugins 下的备份文件夹。
3. 将 ZIP 中的 BepInEx 目录合并到游戏安装目录。最终应在同一目录看到：

```text
游戏安装目录/BepInEx/plugins/SecretFlasherManakaBooBoop/
  SecretFlasherManaka.ForEveryThing.dll
  BooBoopControl.dll
  SecretFlasherManaka.BooBoopBridge.dll
```

4. 配置 `BepInEx/config/local.seleka.booboopbridge.cfg`，首次加载会建立配置；也可提前创建：

```ini
[Device]
PreferredAddress =

[Host]
ExecutablePath =
ProductCachePath =
```

| 配置 | 空值行为 / 自定义方式 |
| --- | --- |
| PreferredAddress | 必须只有一个合格 FN010-RX；多设备时填写明确地址 |
| ExecutablePath | 使用系统 Program Files 下的 Boo Boop/Boo Boop.exe；其他位置填写绝对 EXE 路径 |
| ProductCachePath | 使用当前用户 %APPDATA%/BooBoop/product_list.json；其他位置填写绝对缓存文件路径 |

路径支持环境变量、中文和空格，INI 值无需引号；工作目录从 EXE 路径推导。配置在加载时读取，修改后重新启动游戏。若不希望第一次启动就连接，先填好配置或只安装游戏信号插件。

5. 启动游戏，Bridge 会自动尝试连接一次。完成连接及双输出零值基线后，跟随新的有效游戏观察输出。失败不会重试或重连，需要排查配置 / 日志并重启。

仅观察游戏时，只部署游戏信号 DLL，不部署 Bridge。不要复制系统、Unity、Harmony、BepInEx 或 interop DLL，也不要保留多个同名插件副本。软件写入成功不能证明物理动作或停止，运行设备时保留物理停止手段。

## 开发者：克隆

```powershell
git clone --recurse-submodules https://github.com/LuMianJun/SecretFlasherManaka-BooBoopBridge.git
cd SecretFlasherManaka-BooBoopBridge
```

已克隆的工作区执行 `git submodule update --init --recursive` 补齐固定依赖。

## 开发者：结构与依赖

```text
SecretFlasherManaka-BooBoopBridge/
  SecretFlasherManaka.BooBoopBridge.csproj
  README.md
  build.ps1 / build.sh             # 测试、构建、集中 DLL
  test.ps1 / test.sh               # 不启动设备的回归
  package.ps1                     # 本地 Release ZIP
  src/Plugin.cs                   # 配置、事件订阅、生命周期
  src/BooBoopGameSession.cs        # 映射、状态缓存、串行 worker
  tests/Bridge.MockTests/
  scripts/common.ps1              # 统一依赖定位
  scripts/verify_source.py         # 行为及项目结构检查
  scripts/verify_package.py        # 源码 manifest / 发布 ZIP 检查
  docs/
  dependencies/                   # 两个已固定提交的 Git 子模块
    SecretFlasherManaka-ForEveryThing/
    BooBoopControl/
```

游戏插件只发布纯 CLR 状态，不依赖硬件；硬件库不依赖游戏、Unity 或 BepInEx；本桥接项目拥有映射策略和完整安装包。ForEveryThing 可以被其他硬件 Bridge 复用，BooBoopControl 可在同一仓库继续增加经过验证的型号适配。

生产项目与测试均优先检测仓库内 `dependencies/`，其次使用相邻的两个仓库目录。本仓库通过两个 Git 子模块固定依赖版本；本地也支持相邻目录布局。依赖更新方法见 [仓库组织](docs/GIT_SUBMODULES.md)。

## 构建、测试和打包

需要支持 net6.0 的 SDK / 目标包，运行 mock 还需要命令行 .NET 6 x64 runtime。完整构建引用你自己游戏原有的 BepInEx/core 和 interop，不包含这些依赖，也不要求替换游戏 runtime。

以下在本仓库目录运行：

```powershell
# 只运行 47 项回归，不需要游戏 DLL 或设备。
.\test.ps1

$GameDir = Read-Host '输入包含 BepInEx 的游戏安装目录绝对路径'
# 先测试，再构建；输出三个 DLL 到 artifacts/plugins/SecretFlasherManakaBooBoop/。
.\build.ps1 -GameDir $GameDir
# 先测试、构建，再生成和校验本地发布 ZIP。
.\package.ps1 -GameDir $GameDir
```

`BOOBOOP_GAME_DIR` 也可提供游戏路径。使用任意其他依赖布局时，对 test / build / package 传入 `-GameSignalsProject` 和 `-HardwareControlProject` 两个 csproj 的绝对路径；直接 dotnet 命令对应 `-p:GameSignalsProject=...` / `-p:HardwareControlProject=...`。

```bash
bash test.sh
bash build.sh --game-dir '/实际可读取的游戏目录'
# 自定义依赖时增加 --game-project / --hardware-project。
```

bash 入口可用于源码构建，不表示原厂硬件链路支持 Linux。package.ps1 是 Windows PowerShell 打包入口，ZIP 名为 `SecretFlasherManaka-BooBoopBridge-1.2.0-win-x64.zip`，只包含三个 DLL、安装说明、MIT 许可证和第三方说明，不自动部署或上传。

```powershell
python .\scripts\verify_source.py
python .\scripts\verify_package.py
python .\scripts\verify_package.py --archive .\artifacts\releases\SecretFlasherManaka-BooBoopBridge-1.2.0-win-x64.zip
```

源码 manifest 按独立仓库生成，排除 .git、dependencies 和生成目录。源码变更审查后用 `--update-manifest` 更新；检查另外两个仓库时传 `--source-root`。源码检查可用 `--game-project` / `--hardware-project` 覆盖依赖路径。

## 生命周期、诊断与限制

初始化完成后先双停，再接收新帧；普通 Off / Unknown / 单路来源失效只停止对应通道。一秒没有该路新观察会请求单路置零，空闲 worker 每 100 ms 检查，但在途串行写入可能延迟停止。暂停、场景变化或来源关闭发布全局停止屏障；Bridge 组件禁用永久结束本次会话。退出 / 卸载解除订阅并异步清理，不阻塞 Unity 主线程。

日志为游戏的 `BepInEx/LogOutput.log`：`[Game]` 检查 Hook、Leader 与观察帧，`[Game/Piston UI cache]` 检查活塞缓存，`[Hardware]` 检查连接 / 写入，`[Bridge]` 检查映射与停止。加载、连接、管道写入与物理执行是不同证据。

活塞来源仍是 UI 缓存，没有底层更新时间；持续心跳不能证明执行器在运动。面板唯一性只在恢复查找时校验，缓存有效期间新增第二个面板不会立即被拒绝。历史道具 A/B 差异未定位。47 项 mock 不覆盖 Harmony、IL2CPP、原厂协议或物理停止，游戏与设备实际运行尚未验证。

Bridge 更名后必须移出旧 DLL；旧 Bridge 命名空间的消费者需更新引用并重新编译。游戏 / 硬件接口身份及插件 GUID 保留，详情见 [兼容性](docs/COMPATIBILITY.md)。[验证记录](docs/VALIDATION.md)、[活塞来源](docs/PISTON_SOURCE.md)、[测试说明](tests/Bridge.MockTests/README.md)。



## 许可证

[MIT](LICENSE)，Copyright (c) 2026 Lu_Noodles。外部软件与引用范围见 [第三方说明](THIRD_PARTY_NOTICES.md)。
