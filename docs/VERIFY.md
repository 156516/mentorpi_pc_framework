# 树莓派回来后 — 5 步验证 checklist

> 适用：树莓派上电 → USB 接 PC（或者热点连上） → 等 90 秒
> 目标：5 分钟内确认端到端 ROS2 通讯 OK

---

## 步骤 1：网络通

```bash
ping -c 3 192.168.149.1
```

预期：`3 received, 0% packet loss`。

不通则：
- 检查 PC 是否连了树莓派热点 `HW-9E7168C4`
- `nmcli device wifi connect HW-9E7168C4 password hiwonder`

---

## 步骤 2：PC 容器看到树莓派话题

```bash
docker exec mentorpi_monitor bash -lc \
  'source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 topic list' | head -30
```

预期看到至少：

```
/odom
/imu
/joint_states
/scan
/tf
/tf_static
/ros_robot_controller/battery
/ros_robot_controller/set_motor
/ros_robot_controller/set_led
/ros_robot_controller/imu_raw
/rosout
```

如果只看到 `/parameter_events /rosout` 两个本地话题 → ROS_DOMAIN_ID 不一致或 DDS 跨网段不通。

---

## 步骤 3：monitor 节点收到数据

```bash
docker logs -f mentorpi_monitor
```

预期（不是 `--`，是真值）：

```
bat=12.34 V  85%   pose=(1.23, 0.45, yaw=30°)
imu  R/P/Y=0°/0°/30°  vel=(0.10, 0.00)
cmd  v=0.00m/s w=0.00rad/s
topics: /battery=fresh, /imu=fresh, /odom=fresh, cmd_vel=fresh
```

**`topics: ...fresh` = 端到端成功**。如果都是 `STALE` 或 `topics: --` → 树莓派 bringup 没起来。

---

## 步骤 4：cpp_demo 节点收到 /odom

```bash
docker logs -f mentorpi_cpp_demo
```

预期（默认 launch 没开 publish，只 echo）：

```
[INFO] [odom_echo_node]: odom_echo_node up — topic=/odom print_hz=1.0
[INFO] [odom_echo_node]: x=1.234 y=5.678 yaw=30.5deg
[INFO] [odom_echo_node]: x=1.240 y=5.681 yaw=30.5deg
...
```

如果只有 `waiting for odom...` 一直转 → /odom 没数据。

---

## 步骤 5（可选）：GUI 起得来

```bash
xhost +local:docker
docker compose --profile gui up -d mentorpi_pc_gui
docker logs -f mentorpi_gui
```

预期看到 `.NET` runtime 启动 + Avalonia 应用初始化 + GUI 窗口出现。

如果起不来：
- `docker logs mentorpi_pc_gui` 看 .NET 错误
- GUI 代码已实现（rosbridge WebSocket 客户端），点「连接」后看 `ConnectionStatus` 是否「已连接」

---

## 全 OK 的标志

- ✅ `ping 192.168.149.1` 通
- ✅ `ros2 topic list` 看到 ≥30 个树莓派话题
- ✅ `monitor` 日志 `topics: ...fresh`
- ✅ `cpp_demo` 日志有真实 `x=...yaw=...`
- ✅ （可选）GUI 容器起来，窗口显示电池/速度/IMU

**全部 OK = 系统上线**，可以开始写新算法。

---

## 出问题快速定位

| 现象 | 检查 | 修法 |
|---|---|---|
| ping 不通 | WiFi 是否连 HW-9E7168C4 | 重连 |
| 树莓派话题看不到 | 树莓派容器 alive? | SSH 进 Pi 看 `docker ps` |
| monitor 看不到数据 | 节点订阅代码对吗? | `docker exec mentorpi_monitor bash -lc 'ros2 node info /mentorpi_pc_monitor'` |
| cpp_demo `waiting for odom` | /odom 有发布吗? | `ros2 topic hz /odom` |
| GUI 起不来 | docker log | 检查 rosbridge 容器在跑 + `xhost +local:docker` |

---

## 树莓派未起来时

如果树莓派在充电，`ros2 topic list` 会看到本地话题少：

```
/parameter_events
/rosout
```

这是正常的。等树莓派起来后自动恢复。