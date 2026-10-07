# 三个独立仓库的组织

仓库名是 SecretFlasherManaka-ForEveryThing、BooBoopControl、SecretFlasherManaka-BooBoopBridge。前两项可独立维护，第三项管理桥接与完整安装包。三个公开仓库位于 GitHub 的 LuMianJun 账号下；Bridge 已登记两个真实子模块并固定依赖提交。

本地三个目录可以相邻，直接使用现有脚本。发布后，Bridge 可在 dependencies 下登记两个真实 Git 子模块，固定经过验证的依赖提交。实际 URL 为 https://github.com/LuMianJun/SecretFlasherManaka-ForEveryThing.git 和 https://github.com/LuMianJun/BooBoopControl.git。

以下是首次登记子模块的说明。正常递归克隆本仓库已经具备这两个子模块，不需要重复执行 submodule add：

```powershell
$GameUrl = Read-Host '游戏信号仓库的真实 URL'
$HardwareUrl = Read-Host '硬件仓库的真实 URL'
git submodule add $GameUrl dependencies/SecretFlasherManaka-ForEveryThing
git submodule add $HardwareUrl dependencies/BooBoopControl

$GameRevision = Read-Host '要固定的游戏仓库 commit 或 tag'
$HardwareRevision = Read-Host '要固定的硬件仓库 commit 或 tag'
git -C dependencies/SecretFlasherManaka-ForEveryThing switch --detach $GameRevision
git -C dependencies/BooBoopControl switch --detach $HardwareRevision
.\test.ps1
$GameDir = Read-Host '你的游戏安装目录'
.\build.ps1 -GameDir $GameDir
git diff --submodule=log
```

确认依赖与组合通过验证后，再提交 .gitmodules 和两个依赖指针。生产项目、mock 与脚本会自动优先检测 dependencies，未发现时回退到相邻目录；不同布局也可传入绝对 csproj 路径。

开发者使用 git clone --recurse-submodules 克隆实际 Bridge 仓库，已有克隆使用 git submodule update --init --recursive。普通玩家使用 Bridge 发布的完整 ZIP，不需要子模块。不要将两个依赖的源码再次复制提交到 Bridge，也不要把 BepInEx、游戏 DLL、原厂 EXE 或产品缓存提交到仓库。

父目录的 solution、转发脚本及导航只用于本地工作区，不需要另建第四个仓库。各仓库自己的 build.ps1 / build.sh 随其源码维护，Bridge 的 test / package / docs / scripts 随 Bridge 维护。Git 元数据与依赖不计入各自的 SOURCE_MANIFEST.sha256。
