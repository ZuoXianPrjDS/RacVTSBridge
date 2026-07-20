# RacVTSBridge

赛车游戏加速度数据桥接工具，将游戏中的加速度数据传输到 VTube Studio，实现 Live2D 模型随车辆姿态动态响应。

## 功能特性

- **游戏支持**：极限竞速（Forza）系列，神力科莎（Assetto Corsa）系列，欧洲卡车模拟2（ETS2）和美国卡车模拟（ATS）
- **实时数据传输**：通过 VTube Studio API 注入加速度参数
- **多语言界面**：支持中文/英文切换
- **轻量级**：依赖框架发布，体积小巧

## 支持的游戏

| 游戏 | 数据方式 |
|------|----------|
| Forza Horizon 4 | UDP 数据输出 |
| Forza Horizon 5 | UDP 数据输出 |
| Forza Horizon 6 | UDP 数据输出 |
| Forza Motorsport 7 | UDP 数据输出 |
| Forza Motorsport | UDP 数据输出 |
| Assetto Corsa | 共享内存 |
| Assetto Corsa Competizione | 共享内存 |

## 使用方法

### Forza 系列

1. 在 Forza 游戏设置中开启「数据输出」功能
2. 设置数据输出端口（默认 5300）
3. 在 RacVTSBridge 中选择「极限竞速」模式
4. 输入 UDP 端口号
5. 点击「启动」
6.双机推流功能仅对Forza系列生效。在推流机上改为“接收端”并查询ipv4地址后启动，在游戏主机上改为“发送端”并将目标ip地址改为推流机的ipv4地址后启动。

### Assetto Corsa 系列

1. 启动 Assetto Corsa 或 ACC 游戏
2. 在 RacVTSBridge 中选择「神力科莎系列」模式
3. 点击「启动」，程序会自动连接游戏共享内存

###欧洲卡车模拟2/美国卡车模拟

1.先下载遥测插件，地址github.com/nlhans/ets2-sdk-plugin
2.复制ets2-telemerty.dll到游戏文件夹/bin/win_x64/plugins
3.进入欧卡2/美卡，同意关于遥测的提示
4.在在 RacVTSBridge 中选择「欧洲卡车模拟2」模式
5. 点击「启动」，程序会自动连接游戏共享内存

### VTube Studio 设置

1. 在 VTube Studio 中开启「启用 API」
2. 在「插件设置 → 自定义参数」中添加以下参数：
   - `AccelerationX`：横向加速度
   - `AccelerationZ`：前后加速度
3. 将参数绑定到模型的相应动作

## 系统要求

- Windows 10/11 (x64)
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- VTube Studio

## 开发环境

- Visual Studio 2022
- .NET 8.0 SDK
- WPF

## 依赖库

- [VTS-Sharp](https://github.com/Virtual-Ani/VTS-Sharp) - VTube Studio API 封装
- Newtonsoft.Json - JSON 处理

## 许可证

本项目仅供学习交流使用。

## 致谢

- B站左舷基于 TRAE Solo 创作
- 加q群954463059联系
