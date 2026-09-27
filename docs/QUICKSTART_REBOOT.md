# 重启后快速开始（编译 / 运行代码）

> 适用：框架已经按 `SETUP_FROM_ZERO.md` 搭好，之后每次电脑重启 / 树莓派重启后，怎么最快恢复开发。
> 目标：**3 分钟内恢复到「能编译、能跑、能看数据」**。

---

## 一句话流程

```
连热点 → 树莓派上电(听"滴"声) → bash mentorpi.sh start → bash mentorpi.sh gui → 改代码 → bash mentorpi.sh rebuild -w <模块>
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

## 第 2 步：起 PC 端容器（一键脚本）

`~/mentorpi_pc_framework/mentorpi.sh` 是项目根目录的统一管理脚本，覆盖所有常用操作：

```bash
cd ~/mentorpi_pc_framework
bash mentorpi.sh start    # 起 4 个核心容器 + 验证数据流
bash mentorpi.sh gui      # 起 GUI 控制台
```

`start` 干了 4 件事：

- 自动 build base 镜像（如果没 build 过）
- 起 `monitor` / `cpp_demo` / `obstacle` / `rosbridge` 4 个容器
- 等 5 秒让节点连上 DDS
- 看 `monitor` 日志，自动判断数据流是否 `fresh`

输出示例：

```
>>> 起 4 个核心容器 (monitor / cpp_demo / obstacle / rosbridge)
✓ 数据流通：topics: /odom=fresh, /imu=fresh, /battery=fresh
下一步：bash mentorpi.sh gui    (起 GUI 控制台)
```

---

## 第 3 步：起 GUI 控制台

```bash
bash mentorpi.sh gui
```

- 自动跑 `xhost +local:docker`（第一次需要）
- 起 GUI 容器
- 窗口弹出后点左上角**「连接」**，显示电池 / 速度 / IMU

---

## 第 4 步：改代码 + build + 看日志

### 模块名 cheat sheet

| 你想改的语言 / 文件位置 | 服务名 (给 `mentorpi.sh rebuild` 用) | 容器名 (`docker ps` 看) |
|---------|------------------------|---------|
| Python: `modules/mentorpi_pc_monitor/.../monitor_node.py` | `mentorpi_pc_monitor` | `mentorpi_monitor` |
| Python: `modules/obstacle_distance/.../obstacle_node.py` | `obstacle_distance` | `mentorpi_obstacle` |
| **C++**: `modules/mentorpi_pc_cpp_demo/src/*.cpp` | `mentorpi_pc_cpp_demo` | `mentorpi_cpp_demo` |
| **C#**: `services/gui/*.cs` / `*.axaml` | `mentorpi_pc_gui` | `mentorpi_gui` |
| WebSocket 桥 / rosbridge | `rosbridge` | `mentorpi_rosbridge`（无需改）|
| URDF / RViz | `mentorpi_description` | `mentorpi_description`（按需）|

### 改完代码，一条命令搞定

```bash
# Python 节点
bash mentorpi.sh rebuild -w mentorpi_pc_monitor

# C++ 节点
bash mentorpi.sh rebuild -w mentorpi_pc_cpp_demo

# GUI（C#）—— 更快的选择：跳过 docker，宿主直接跑（见下方"GUI 特殊路径"）
bash mentorpi.sh rebuild -w mentorpi_pc_gui
```

`-w` 含义：**build + 重启容器 + 自动接 `docker compose logs -f`**，按 `Ctrl+C` 退出日志（容器继续跑）。

不带 `-w` 就是 build + 重启不跟日志，自己 `bash mentorpi.sh logs <模块>`。

---

### GUI 特殊路径（最快反馈）

C# GUI 在宿主的 .NET 8 SDK 直接跑，不用每次 docker build：

```bash
cd ~/mentorpi_pc_framework/services/gui
dotnet run       # build + 弹窗
```

或者 VSCode 里 `F5` → 选 `GUI: dotnet run (宿主)`。

要发布 docker 镜像才用 `mentorpi.sh rebuild -w mentorpi_pc_gui`。

---

## 第 5 步：验证数据通了

```bash
# 看 monitor 日志（最近一行）
docker logs --tail 3 mentorpi_monitor
# 期望最后一行：topics: /odom=fresh /imu=fresh /battery=fresh

# 看话题发布频率（在 monitor 容器里）
docker exec mentorpi_monitor bash -lc \
  'source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 topic hz /odom'

# 一眼看清所有状态
bash mentorpi.sh status
```

`/odom` 应该 ~30 Hz。`/imu` `/battery` 有数据说明树莓派 STM32 也正常。

---

## VSCode 工作流

打开项目：

```bash
code ~/mentorpi_pc_framework                # 顶层（Python/C++/所有模块）
code ~/mentorpi_pc_framework/services/gui   # GUI 子项目（dotnet 智能提示更好）
```

改完代码，**两种方式 build**：

### 方式 A：VSCode Tasks（推荐）

`Ctrl+Shift+P` → `Tasks: Run Task` → 选：

| Task 标签 | 干什么 |
|----------|------|
| **`🔁 build + up + logs (current module)`** | 自动识别当前打开的文件所属模块，build + 重启 + 跟日志 |
| `docker compose build (current module)` | 只 build |
| `docker compose logs (current module)` | 只跟日志 |
| `dotnet run services/gui (宿主直接跑)` | GUI 宿主跑（最快反馈）|

### 方式 B：终端

`Ctrl+`` 开终端，输上面的 `mentorpi.sh rebuild -w <模块名>`。

---

## 常见坑（按遇到概率排序）

| 现象 | 原因 | 修法 |
|------|------|------|
| GUI 连上了但数据全 0 | rosbridge 容器跑久了内部线程卡死 | `docker compose restart rosbridge`，重连 |
| 收不到话题数据 | QoS 不匹配（订阅端必须 reliable） | 已修：所有节点都用 `QoSProfile(reliability=ReliabilityPolicy.RELIABLE)` |
| `/odom` 有、`/imu` `/battery` 没 | 树莓派 STM32 没正常起（没「滴」声）| 树莓派重新上电，听滴声 |
| 驱动报 `No such file or directory: '/dev/rrc'` | STM32 被识别成 ttyUSB 而非 ttyACM，udev 没建 /dev/rrc | 见 `SETUP_FROM_ZERO.md` 附录「串口设备名」|
| 树莓派 ping 不通 | 热点没开 / PC 没连热点 | 重连 `HW-9E7168C4` |
| `docker compose build` 卡在 restore | NuGet 源慢 | 已配国内镜像，耐心等或换源 |
| GUI `dotnet run` 抛 `Default font family name can't be null or empty` | Avalonia 11.0 的 `Avalonia.Fonts.Inter` 包**没塞字体**，在 Linux 下崩 | `Gui.csproj` 升到 **11.3.22**（已升级） |
| monitor `bat=12.88V 79.45%` 显示奇怪 | /battery 实际发 `UInt16`，raw 是百分比 ×100 | 已修，自动归一化（见下面 Bug #4）|

---

## 已修复的关键 Bug（升级记录）

### 1. rosbridge 收不到 /odom /imu — QoS reliable 缺失

**症状**：树莓派 bringup 起来，`ros2 topic hz /odom` 在 PC 端能看到 30Hz，但 GUI 连上 rosbridge 后电池 / 速度全是 0。

**原因**：`services/gui/Services/RosService.cs` 之前 `subscribe` 没带 `qos.reliability=reliable`；rosbridge 默认 best_effort，但 HiWonder 驱动节点用 RELIABLE 发布，订阅端必须显式指定 reliable 才能匹配。

**修复**：`SubscribeAsync` 加 `qos = new { reliability = "reliable" }`，参见 `services/gui/Services/RosService.cs:53-66`。

**经验**：任何时候 rosbridge 收不到 native ROS 能看到的话题，先怀疑 QoS 不匹配。

### 2. Avalonia 11.0 Inter 字体空壳

**症状**：`dotnet run services/gui` 抛 `System.InvalidOperationException: Default font family name can't be null or empty`，窗口起不来。

**原因**：NuGet 上 `Avalonia.Fonts.Inter` 11.0.0 包只有 5KB 的 DLL shim，**没有任何字体文件**——Avalonia 11.0 本身不内置默认字体。

**修复**：升级到 **11.3.22**（Inter.ttf 内嵌进 DLL，宿主 / docker / Windows 都直接跑）。当前 `Gui.csproj` 已固定在 `11.3.22`。

### 3. PC 端 native ROS 节点也收不到 /odom /imu /battery — QoS 不匹配

**症状**：`ros2 topic hz /odom` 在 PC 端能直接看到 30Hz，但 `mentorpi_monitor` 容器里的 monitor_node 一直 `topics: --` 或只偶尔出现 fresh。

**原因**：HiWonder 树莓派端所有 ROS 节点都用 **RELIABLE** 发布话题，但 ROS2 默认订阅端是 **BEST_EFFORT**——QoS 不匹配，订阅端永远收不到。

**修复**：
- Python 节点（`monitor_node.py` / `dashboard_node.py` / `obstacle_node.py`）：用 `QoSProfile(reliability=ReliabilityPolicy.RELIABLE, depth=10)`
- C++ 节点（`odom_echo_node.cpp` / `odom_pub_node.cpp`）：用 `rclcpp::QoS(KeepLast(10)).reliability(rclcpp::ReliabilityPolicy::Reliable)`

**经验**：所有 HiWonder ROS 节点都用 RELIABLE，订阅端必须显式 RELIABLE 才行。这是上面 rosbridge 那条的全链路延伸——native ROS → rosbridge → GUI 三段都要靠 RELIABLE 串起来。

### 4. /ros_robot_controller/battery 实际发的是 std_msgs/UInt16，不是 BatteryState

**症状**：mentorpi_monitor 一直 `bat=-- --`，明明 `ros2 topic info` 能看到 publisher。

**原因**：HiWonder 镜像里这个 topic 是**双类型**（BatteryState + UInt16），但**实际 publisher 发的是 UInt16**，`msg.data` 又是**百分比 × 100**（如 7948 = 79.48%），不是字面电压。

**修复**：
- 订阅端类型改成 `std_msgs/UInt16`
- 自动归一化 raw 数值：`0~100` 当百分比、`100~10000` 当百分比 × 100、`> 10000` 当 mV
- 12V 铅酸经验估算电压：10.5V 截止 → 13.5V 满电

---

## 完全关机再开机的顺序（记牢）

1. **开**：开热点 → 树莓派（听滴声）→ `bash mentorpi.sh start` → `bash mentorpi.sh gui`
2. **关**：`bash mentorpi.sh stop` → 关 VSCode → 关 PC → 拔树莓派电（**最后拔！**）

```bash
# 关机命令
bash mentorpi.sh stop
```

---

## 常用命令速记

### 一键脚本（推荐）

```bash
bash mentorpi.sh start                     # 起 4 个核心容器 + 验证数据流
bash mentorpi.sh gui                       # 起 GUI
bash mentorpi.sh status                    # 一眼看所有状态
bash mentorpi.sh logs [模块]               # 跟日志（默认 monitor）
bash mentorpi.sh rebuild [模块]            # 重 build + 重启
bash mentorpi.sh rebuild -w [模块]         # 重 build + 重启 + 自动跟日志
bash mentorpi.sh stop                      # 停所有容器
bash mentorpi.sh rebuild                   # 重 build 全部
```

### 底层 docker 命令（备用）

```bash
docker compose ps                        # 看所有容器状态
docker compose logs -f <服务名>          # 跟日志
docker compose build <服务名>            # 重编一个模块
docker compose up -d <服务名>            # 重启一个模块
docker compose restart rosbridge         # 数据全 0 时的第一招
docker exec -it <容器名> bash            # 进容器调试
```

---

## 加新算法模块

参见 `modules/_template/README.md`。

10 分钟接入：

```bash
# 1. 拷模板
cp -r modules/_template modules/my_cool_algo

# 2. 改名（_template → my_cool_algo）
cd modules/my_cool_algo
mv _template my_cool_algo  # 嵌套目录重命名
mv resource/_template resource/my_cool_algo
mv my_cool_algo/_template my_cool_algo/__init__.py 2>/dev/null

# 3. 改 package.xml / setup.py 里的 my_cool_algo
sed -i 's/my_cool_algo/你的包名/g' package.xml setup.py setup.cfg

# 4. 写节点代码
# modules/my_cool_algo/my_cool_algo/my_node.py

# 5. 注册到 setup.py 的 entry_points

# 6. 加进 docker-compose.yml:
#   my_cool_algo:
#     build:
#       context: .
#       dockerfile: modules/my_cool_algo/Dockerfile
#     network_mode: host
#     environment: [ROS_DOMAIN_ID=0]

# 7. 启动
bash mentorpi.sh rebuild -w my_cool_algo
```

### 订阅 / 发布话题示例

常用话题（树莓派 bringup 自动发布）：

| 话题 | 类型 | 用途 |
|------|------|------|
| `/odom` | `nav_msgs/Odometry` | 里程计（位置 + 速度）|
| `/imu` | `sensor_msgs/Imu` | IMU 姿态 |
| `/scan` | `sensor_msgs/LaserScan` | 2D 激光 |
| `/cmd_vel` | `geometry_msgs/Twist` | 控制车 |
| `/ros_robot_controller/battery` | `std_msgs/UInt16` | 电池百分比（×100，见 Bug #4）|

订阅示例（参考 `monitor_node.py` 的写法）：

```python
from rclpy.qos import QoSProfile, ReliabilityPolicy
from sensor_msgs.msg import Imu

reliable_qos = QoSProfile(reliability=ReliabilityPolicy.RELIABLE, depth=10)
self.create_subscription(Imu, '/imu', self.on_imu, reliable_qos)
```

发布 `/cmd_vel`：

```python
from geometry_msgs.msg import Twist
self.cmd_pub = self.create_publisher(Twist, '/cmd_vel', reliable_qos)

# 10 Hz 发一次
self.create_timer(0.1, lambda: self.cmd_pub.publish(
    Twist linear=Twist(linear_x=0.1)))  # 0.1 m/s 前进
```

完整端到端示例（Python / C++ / GUI 三种）见 `docs/EXAMPLES.md`。

### 容器内调试技巧

```bash
# 进容器 shell
bash mentorpi.sh exec                              # 默认进 monitor
docker exec -it mentorpi_cpp_demo bash             # 进指定容器

# 在容器内
source /opt/ros/humble/setup.bash
source /workspace/install/setup.bash
ros2 topic list                                    # 看话题
ros2 topic hz /odom                                # 测频率
ros2 topic echo /imu --once                        # 看一帧数据
ros2 run rqt_graph rqt_graph                       # 节点图（需 X11）
ros2 run <你的包名> <节点> --ros-args -p print_hz:=2.0  # 手动跑节点
```

---

## 端到端 5 步验证（树莓派上电后）

刚把树莓派接上 / 重新上电，按这 5 步 5 分钟内确认全链路 OK：

**Step 1：网络**

```bash
ping -c 3 192.168.149.1
```

期望 `0% packet loss`。不通 → PC WiFi 重连 `HW-9E7168C4`。

**Step 2：PC 看到树莓派话题**

```bash
docker exec mentorpi_monitor bash -lc \
  'source /opt/ros/humble/setup.bash && \
   source /workspace/install/setup.bash && \
   ros2 topic list' | head -30
```

期望至少看到 `/odom /imu /joint_states /scan /tf /ros_robot_controller/battery /rosout` 等。

**Step 3：monitor 收到数据**

```bash
docker logs -f mentorpi_monitor
```

期望（不是 `--`，是真值）：
```
bat=12.34V 85.00%   pose=(1.23, 0.45, yaw=30°)
imu  R/P/Y=0°/0°/30°  vel=(0.10, 0.00)
topics: /battery=fresh, /imu=fresh, /odom=fresh
```

`topics: ...fresh` = 端到端成功。`STALE` 或 `--` → 树莓派没起来。

**Step 4：cpp_demo 收到 /odom**

```bash
docker logs -f mentorpi_cpp_demo
```

期望：`x=... y=... yaw=...`。只有 `waiting for odom...` → /odom 没数据。

**Step 5（可选）：GUI 起来**

```bash
bash mentorpi.sh gui
# 看桌面窗口点「连接」
```

**全 OK 标志**：
- ✅ ping 通
- ✅ `ros2 topic list` ≥30 个话题
- ✅ monitor `topics: ...fresh`
- ✅ cpp_demo 有真实坐标
- ✅ GUI 弹窗点连接显示数据

---

## 30 秒速查（按场景）

| 想做什么 | 一条命令 |
|---------|--------|
| 改 Python 节点 | `code ~/mentorpi_pc_framework` → 改 `.py` → `bash mentorpi.sh rebuild -w mentorpi_pc_monitor` |
| 改 C++ 节点 | 同上 → `bash mentorpi.sh rebuild -w mentorpi_pc_cpp_demo` |
| 改 GUI（C#） | `code services/gui` → 改 `.cs/.axaml` → `F5` → "GUI: dotnet run (宿主)" |
| 看 PC 端 ROS 数据 | `docker exec mentorpi_monitor bash -lc 'source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 topic list'` |
| 看某个容器实时日志 | `bash mentorpi.sh logs <模块名>` |
| 调试节点（容器内） | `bash mentorpi.sh exec <模块名>` → 容器内 `ros2 topic hz /odom` |
| 进容器手动跑节点 | `docker exec -it mentorpi_xxx bash` → `source /opt/ros/humble/setup.bash && ros2 run <pkg> <node>` |
| 重 build 所有镜像 | `docker compose build --no-cache` |
| 删所有容器+镜像（重置）| `docker compose down --rmi all` ⚠️ 不可逆 |
| 看所有状态 | `bash mentorpi.sh status` |
| 停所有 | `bash mentorpi.sh stop` |

### 网络问题排查

| 现象 | 步骤 |
|------|------|
| 收不到树莓派话题 | `ping 192.168.149.1` |
| 树莓派热点没连 | `nmcli device wifi connect HW-9E7168C4 password hiwonder` |
| ROS_DOMAIN_ID 不一致 | 检查 `docker-compose.yml` 每个 service 都是 `ROS_DOMAIN_ID=0` |
| DDS 收不到但 ICMP 通 | 换 Cyclone DDS：`RMW_IMPLEMENTATION=rmw_cyclonedds_cpp` |

### 镜像/容器生命周期

```
docker compose build              # build 镜像
docker compose up -d              # 起容器（自动 build 如果镜像不存在）
docker compose down               # 停容器（保留镜像）
docker compose down --rmi all     # 停容器 + 删镜像
docker compose down -v             # 停容器 + 删卷
```