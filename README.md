# UtilityApp

用于丐版UPS的断电关机功能实现,适用于UPS没有管理驱动的情况

## 原理

监控网络网关可达性并在检测到网络断电后自动关机。它可以帮助您在断电情况下安全地关闭计算机，防止数据丢失和硬件损坏。

## 功能特性

- **网关监控**：定期检查指定的网关地址是否可达。
- **自动关机**：当网关在设定的时间阈值内不可达时，触发关机流程。
- **关机倒计时**：在关机前显示倒计时窗口，允许用户取消关机操作。
- **调试模式**：提供调试选项，可在测试时防止实际关机。

## 使用说明

### 主界面

应用程序的主界面包括以下控件：

- **网关地址**：要监控的网关 IP 地址或主机名。
- **检查间隔**：以毫秒为单位，设置定期检查网关可达性的时间间隔。
- **断电时间阈值**：以毫秒为单位，网关不可达的持续时间，超过此阈值将触发关机流程。
- **启用检测**：勾选此选项以启用网关监控功能。
- **调试模式**：勾选此选项以启用调试模式，防止实际关机，仅用于测试。
- **状态显示**：显示当前监控状态和操作信息。
- **日志窗口**：实时显示应用程序的运行日志和事件信息。
- **应用设置**：点击“应用”按钮手动保存设置。

### 监控流程

1. **启用检测**：
   - 勾选“启用检测”复选框，应用程序将开始按照设定的检查间隔监控指定的网关地址。

2. **网关可达性检查**：
   - 应用程序使用 Ping 命令检查网关的可达性。
   - 如果网关可达，状态显示为“网关可达”，并重置断电计时。

3. **断电检测**：
   - 如果网关不可达，开始记录断电开始时间。
   - 如果网关在断电时间阈值内恢复可达，断电计时重置。
   - 如果超过断电时间阈值，触发关机流程。

4. **关机流程**：
   - **正常模式**：
     - 显示关机倒计时窗口，默认倒计时 30 秒。
     - 用户可以在倒计时期间取消关机操作。
   - **调试模式**：
     - 如果启用了调试模式，应用程序将弹出消息框提示“已关机（调试模式）”，但不会实际关机。

5. **关机执行**：
   - 如果用户未取消关机，倒计时结束后，应用程序将执行关机命令，安全关闭计算机。

## 注意事项

- **管理员权限**：执行关机命令需要应用程序具有适当的权限，建议以管理员身份运行。
- **防火墙设置**：确保防火墙允许应用程序发送 ICMP（Ping）请求。
- **网络环境**：在使用应用程序时，请确保网络环境稳定，避免误判。
