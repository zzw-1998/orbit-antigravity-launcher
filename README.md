# 星轨启动器

星轨是 Google Antigravity 桌面版的第三方 Windows 启动器。双击一次，即可通过本地 HTTP 代理启动 Antigravity，并在设置、首页导航和插件市场上显示中文译文。从仓库首页的 Releases 下载最新版本的 **OrbitLauncher.exe** 即可运行。

## 启动与切换

1. 安装官方 Antigravity 桌面版，并开启自己的代理客户端。启动器预设使用本机 HTTP 或混合端口 **6478**。如果检测失败，在窗口里改为实际端口后点「启动反重力」。端口选择仅在本次启动器运行期间有效。
2. 双击 EXE。启动器会查找 Antigravity.exe，检测本机端口，并给新进程设置 HTTP_PROXY 和 HTTPS_PROXY。若桌面应用已经运行，可以立即切换中文显示。要让它继承新的代理环境，请先保存工作并完全退出 Antigravity，然后再启动。
3. 「启用中文设置」默认开启。可用 **Ctrl + Alt + F8** 切换，或双击系统托盘里的星轨图标打开启动器。关闭启动器窗口会收起到托盘，结束运行请选托盘菜单里的「退出启动器」。

## 中文覆盖

启动器通过 Windows UI Automation 定位可见控件，从目标窗口画面取色，然后在允许鼠标穿透的独立窗口里绘制中文。翻译词表随 EXE 一起打包，在本机匹配。不需要把界面文本交给在线翻译服务，也无需修改 Antigravity 安装文件。开关偏好保存在当前用户注册表的 Software\OrbitAntigravityLauncher。

设置页包含通用、应用、外观、快捷键、通知、模型、浏览器、项目及账户等页面。首页和插件页主要翻译导航、操作和已收录的说明，保留应用名、模型名和插件名。打开「中文设置速查」可查看内置的离线说明。

## 兼容性

此前在 Windows 上用 Antigravity 桌面版 **2.32.2** 测试过设置切换、长页滚动、首页和插件市场。客户端更新、显示缩放或新增插件可能影响文字匹配和位置。未收录的文字保留英文。需要核对原文时可随时暂停覆盖层。

端口检测验证本机 TCP 端口是否可连接。实际代理协议及外部服务连通性取决于代理客户端和 Antigravity 各组件的网络实现。

## 从源码编译

使用 Windows 自带的 .NET Framework 4.x 编译器、Windows Forms 和 UI Automation 程序集。在仓库根目录打开 PowerShell 并运行：

```powershell
.\build.ps1
```

输出为 `dist\OrbitLauncher.exe`。依赖均来自 Windows .NET Framework，编译时无需下载 NuGet 包。代理启动和托盘逻辑位于 `src/Launcher.cs`，控件定位和译文绘制位于 `src/ChineseLayer.cs`。要补译文，请编辑 `resources/zh.tsv`，每行依次写英文、一个制表符和中文，然后重新编译。

发布附件提供 SHA-256 校验值，可使用 `Get-FileHash .\OrbitLauncher.exe` 核对下载文件。EXE 未作代码签名。
