# GUI — C# Avalonia 跨平台控制台

> 用 **Avalonia**（.NET 8）+ **rosbridge WebSocket** 显示车端状态（电池 / 速度 / IMU）。
> 不依赖 ROS2 native 库，Windows / macOS / Linux 都能编译运行。

---

## 架构

```
GUI (Avalonia, C#)
  └── RosService (Websocket.Client)
        │  ws://localhost:9090
        ▼
   rosbridge 容器（rosbridge_server）
        │  ROS_DOMAIN_ID=0
        ▼
   ROS2 DDS（树莓派 /odom /imu /battery）
```

| 文件 | 作用 |
|------|------|
| `Gui.csproj` | 项目定义（Avalonia 11 + CommunityToolkit.Mvvm + Websocket.Client）|
| `Program.cs` | Avalonia 入口 |
| `App.axaml(.cs)` | 应用初始化，创建 RosService 传给 MainWindow |
| `Services/RosService.cs` | rosbridge 客户端封装（connect / subscribe / publish / unsubscribe）|
| `Messages/RosMessages.cs` | ROS 消息 DTO（BatteryState / Odometry / Imu …）|
| `ViewModels/MainViewModel.cs` | 订阅话题 → 刷新可绑定属性 |
| `Views/MainWindow.axaml(.cs)` | 界面布局 |

---

## 关键设计：为什么用 rosbridge 而不是 rclnet

- **跨平台**：原生 rcl 绑定（rclnet）依赖 C++ 的 rcl 库，Linux 专用、部署麻烦。
- **简单**：rosbridge 就是标准 WebSocket + JSON，任何语言都能连。
- **解耦**：GUI 只关心 `ws://` 一个端口，不用 source ROS 环境。

后续要性能（高频话题）再考虑换 rclnet，接口层 `RosService` 已经隔离好。

---

## 本地开发（宿主 .NET 8 SDK）

```bash
cd ~/mentorpi_pc_framework/services/gui

# 用国内镜像 restore
dotnet restore Gui.csproj --source https://nuget.azure.cn/v3/index.json
dotnet build Gui.csproj

# 跑（需要先起 rosbridge：docker compose up -d rosbridge）
dotnet run
```

## Docker 跑

```bash
cd ~/mentorpi_pc_framework
xhost +local:docker
docker compose --profile gui build mentorpi_pc_gui
docker compose --profile gui up -d mentorpi_pc_gui
docker logs -f mentorpi_gui
```

---

## rosbridge 协议速记

客户端连上后发 JSON 帧：

```json
{"op":"subscribe",  "topic":"/odom", "type":"nav_msgs/msg/Odometry"}
{"op":"advertise",  "topic":"/cmd_vel", "type":"geometry_msgs/msg/Twist"}
{"op":"publish",    "topic":"/cmd_vel", "msg":{"linear":{"x":0.1,"y":0,"z":0},"angular":{"x":0,"y":0,"z":0}}}
{"op":"unsubscribe","topic":"/odom"}
```

收到（订阅的回调）：

```json
{"op":"publish","topic":"/odom","msg":{ ...消息体... }}
```

类型名用 ROS2 格式：`<pkg>/msg/<Msg>`（如 `sensor_msgs/msg/BatteryState`）。

---

## 加一个新话题显示

1. 在 `Messages/RosMessages.cs` 加对应 DTO（只声明用到的字段即可，其余自动忽略）。
2. 在 `MainViewModel.cs` 加一个 `[ObservableProperty]` 属性。
3. 在 `SubscribeTopicsAsync()` 里加一段：
   ```csharp
   await _ros.SubscribeAsync<MyMsg>("/my_topic", "my_pkg/msg/MyMsg", msg =>
   {
       MyProp = msg.SomeField;
   });
   ```
4. 在 `MainWindow.axaml` 加一个 `TextBlock` 绑定 `MyProp`。
5. `dotnet build` 通过后 `docker compose --profile gui build mentorpi_pc_gui`。

---

## 故障排查

| 症状 | 排查 |
|------|------|
| 「连接失败」 | rosbridge 容器起了吗：`docker compose up -d rosbridge`；`docker logs mentorpi_rosbridge` |
| 连上但没数据 | 树莓派 bringup 起了吗、话题有发布吗：`docker exec mentorpi_monitor bash -lc 'ros2 topic hz /odom'` |
| restore 慢/失败 | 换国内镜像：`dotnet restore --source https://nuget.azure.cn/v3/index.json` |
| 窗口起不来 | `xhost +local:docker`；`docker logs mentorpi_gui` 看 .NET 错误 |
