# 重启后快速开始（编译 / 运行代码）

> 适用：框架已经按 `SETUP_FROM_ZERO.md` 搭好，之后每次电脑重启 / 树莓派重启后，怎么最快恢复开发。
> 目标：3 分钟内恢复到「能编译、能跑、能看数据」。

---

## 一句话流程

```
连热点 → 树莓派上电(听"滴"声) → docker compose up -d → 改代码 → build → up → 看日志
```

---

## 第 0 步：网络

- 手机开 WiFi 热点 `HW-9E7168C4`（树莓派会自动连）
- PC 的 WiFi 也连同一个热点（拿到 `192.168.149.x`）
- 验证：`ping 192.168.149.1` 通 = 树莓派在线

> 树莓派 IP 固定 `192.168.149.1`。PC 如果同时走 USB tether 上网，不影响（两段网络共存）。

---

## 第 1 步：树莓派上电

- 树莓派通电，等 ~30 秒
- **听「滴」一声**——这是 STM32 主板正常自检的标志
- 没滴声 = STM32 没起来，后续 `/imu` `/battery` 会没数据（见文末「常见坑」）

```bash
ping -c 2 192.168.149.1   # 应该 0% 丢包
```

---

## 第 2 步：起 PC 端容器

```bash
cd ~/mentorpi_pc_framework
docker compose up -d
docker compose ps          # 看所有模块状态，应该都是 Up
```

默认起：`monitor` / `cpp_demo` / `obstacle_distance` / `rosbridge`。
`description`（URDF/RViz）和 `gui` 默认不起，按需单独起。

### 起 GUI 控制台（看数据的面板）

```bash
bash ~/mentorpi_pc_framework/start_gui.sh
```

窗口弹出后点左上角**「连接」**，显示电池 / 速度 / IMU。

---

## 第 3 步：编译运行你的代码

框架里每个功能模块 = 一个独立容器，改哪个就重编哪个。

```bash
cd ~/mentorpi_pc_framework

# 改完代码（比如 modules/mentorpi_pc_monitor/.../monitor_node.py）后：
docker compose build mentorpi_pc_monitor    # 只重编这一个模块
docker compose up -d mentorpi_pc_monitor     # 用新镜像重启它

# 看它日志（确认起来 + 数据流）
docker compose logs -f mentorpi_pc_monitor
```

| 模块 | compose 服务名 | 容器名 | 语言 |
|------|--------------|--------|------|
| 监控节点 | `mentorpi_pc_monitor` | `mentorpi_monitor` | Python |
| C++ 演示 | `mentorpi_pc_cpp_demo` | `mentorpi_cpp_demo` | C++ |
| 障碍物距离 | `obstacle_distance` | `mentorpi_obstacle` | Python |
| WebSocket 桥 | `rosbridge` | `mentorpi_rosbridge` | 无需改 |
| GUI 控制台 | `mentorpi_pc_gui` | `mentorpi_gui` | C# |
| URDF/描述 | `mentorpi_description` | `mentorpi_description` | 按需 |

**改 C++ 模块**：同上，但 `build` 会慢一点（要 cmake + colcon，~几十秒）。
**改 GUI**：`docker compose --profile gui build mentorpi_pc_gui`，再 `start_gui.sh`。

---

## 第 4 步：验证数据通了

```bash
# 看 monitor 日志，最后一行 topics: ... 后面出现 fresh 就是收到数据了
docker logs --tail 3 mentorpi_monitor
# 期望：topics: /odom=fresh /imu=fresh /battery=fresh ...

# 或直接测话题发布频率（在 monitor 容器里）
docker exec mentorpi_monitor bash -lc \
  'source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 topic hz /odom'
```

`/odom` 应该 ~30 Hz。`/imu` `/battery` 有数据说明树莓派 STM32 也正常。

---

## 常见坑（按遇到概率排序）

| 现象 | 原因 | 修法 |
|------|------|------|
| GUI 连上了但数据全 0 | rosbridge 容器跑久了内部线程卡死 | `docker compose restart rosbridge`，重连 |
| 收不到话题数据 | QoS 不匹配（订阅端必须 reliable） | 代码里订阅用 `qos_profile_system_default`（已默认）|
| `/odom` 有、`/imu` `/battery` 没 | 树莓派 STM32 没正常起（没「滴」声）| 树莓派重新上电，听滴声 |
| 驱动报 `No such file or directory: '/dev/rrc'` | STM32 被识别成 ttyUSB 而非 ttyACM，udev 没建 /dev/rrc | 见 `SETUP_FROM_ZERO.md` 附录「串口设备名」|
| 树莓派 ping 不通 | 热点没开 / PC 没连热点 | 重连 `HW-9E7168C4` |
| `docker compose build` 卡在 restore | NuGet 源慢 | 已配国内镜像，耐心等或换源 |

---

## 完全关机再开机的顺序（记牢）

1. **开**：热点 → 树莓派（听滴声）→ PC 容器 → GUI
2. **关**：`docker compose down` → 关 PC → 树莓派断电

## 常用命令速记

```bash
docker compose ps                        # 看所有容器状态
docker compose logs -f <服务名>          # 跟日志
docker compose build <服务名>            # 重编一个模块
docker compose up -d <服务名>            # 重启一个模块
docker compose restart rosbridge         # 数据全 0 时的第一招
docker exec -it <容器名> bash            # 进容器调试
```
