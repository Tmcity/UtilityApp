# UtilityApp

一个小程序，用于丐版UPS的断电关机功能实现,适用于UPS没有管理驱动的情况

## 原理

监控网络网关可达性并在检测到网络断电后自动关机。它可以帮助您在断电情况下安全地关闭计算机，防止数据丢失和硬件损坏。

## 功能特性

- **网关监控**：定期检查指定的网关地址是否可达。
- **自动关机**：当网关在设定的时间阈值内不可达时，触发关机流程。
- **关机倒计时**：在关机前显示倒计时窗口，允许用户取消关机操作。
- **调试模式**：提供调试选项，可在测试时防止实际关机。
- **开机自启**: 开机自启动,无需手动启动
- **日志记录**：实时记录应用程序的运行日志和事件信息。
- **后台运行**：应用程序可以最小化到系统托盘，后台运行。

## 使用说明

### 主界面

应用程序的主界面包括以下控件：

- **网关地址**：要监控的网关 IP 地址或主机名。
- **检查间隔**：以毫秒为单位，设置定期检查网关可达性的时间间隔。
- **断电时间阈值**：以毫秒为单位，网关不可达的持续时间，超过此阈值将触发关机流程。
- **启用检测**：勾选此选项以启用网关监控功能。
- **开机自启**：勾选此选项启用开机自启
- **调试模式**：勾选此选项以启用调试模式，防止实际关机，仅用于测试。
- **状态显示**：显示当前监控状态和操作信息。
- **日志窗口**：实时显示应用程序的运行日志和事件信息。
- **应用设置**：点击“应用”按钮手动保存设置。

### 监控流程

1. **启动应用**：
   - 双击应用程序图标启动应用程序，关闭页面应用程序将最小化到系统托盘。
   - 右键单击托盘图标，可以打开主界面、退出应用程序或查看关于信息。
   - 应用程序将自动加载上次保存的设置，无需手动设置。
   - 如果开启了开机自启,应用程序将在开机后自动启动

2. **启用检测**：
   - 勾选“启用检测”复选框，应用程序将开始按照设定的检查间隔监控指定的网关地址。

3. **网关可达性检查**：
   - 应用程序使用 Ping 命令检查网关的可达性。
   - 如果网关可达，状态显示为“网关可达”，并重置断电计时。

4. **断电检测**：
   - 如果网关不可达，开始记录断电开始时间。
   - 如果网关在断电时间阈值内恢复可达，断电计时重置。
   - 如果超过断电时间阈值，触发关机流程。

5. **关机流程**：
   - **正常模式**：
     - 显示关机倒计时窗口，默认倒计时 120 秒。
     - 用户可以在倒计时期间取消关机操作。
   - **调试模式**：
     - 如果启用了调试模式，应用程序将弹出消息框提示“已关机（调试模式）”，但不会实际关机。

6. **关机执行**：
   - 如果用户未取消关机，倒计时结束后，应用程序将执行关机命令，安全关闭计算机。

## 注意事项

- **管理员权限**：执行应用程序所需的权限不需要管理员，不建议以管理员身份运行。
- **防火墙设置**：确保防火墙允许应用程序发送 ICMP（Ping）请求。
- **网络环境**：在使用应用程序时，请确保网络环境稳定，避免误判。
- **关闭应用**：在不需要时，请在托盘右键关闭应用程序。
