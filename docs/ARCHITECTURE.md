# 架构详解

## 模块化原则

每个 ROS 功能包 = 一个独立 Docker 容器：

```
modules/<pkg_name>/
├── Dockerfile              # FROM mentorpi_pc/ros2-base + COPY + colcon build
├── package.xml             # ROS2 标准
├── setup.py                # ament_python
├── resource/<pkg>          # package marker
├── <pkg>/                  # Python 源码
└── launch/                 # launch 文件
```

**为什么独立**：

1. **隔离性**——一个模块崩了不影响其他
2. **可独立 build**——改一个模块只重 build 它，不用全栈重编
3. **可独立发布**——每个模块有自己的镜像，可单独升级
4. **多语言友好**——Python / C++ / Rust / C# 都能写

## 容器间通讯

```
所有模块 + 树莓派 + rosbridge + GUI
         │
         │ ROS_DOMAIN_ID=0
         │ DDS 组播 (UDP, port 7400)
         │
         ▼
共享 ROS2 网络
```

**关键配置**：

```yaml
services:
  <module>:
    network_mode: host       # 用宿主网络栈，不隔离
    environment:
      - ROS_DOMAIN_ID=0      # 必须统一
```

## GUI 接入策略

C# GUI 在 `services/gui/`，**不依赖** ROS2 native 库——通过 rosbridge (WebSocket) 跟 ROS2 通讯。

```
gui (C# Avalonia) ──ws://──> rosbridge (container) ──> ROS2 DDS
```

**好处**：

- GUI 镜像体积小（runtime only，不需要 ROS2 native）
- Windows / Mac / Linux 都能写 GUI
- 后续使用者**不用懂 ROS2**，只用 RosBridgeNet 客户端

## Build 流程

```
host: docker compose build
    │
    ├─> monitor (Python 镜像)
    │     └─> build: modules/mentorpi_pc_monitor/
    │           └─> colcon build --packages-select mentorpi_pc_monitor
    │
    ├─> cpp_demo (C++ 镜像)
    │     └─> build: modules/mentorpi_pc_cpp_demo/
    │           └─> colcon build --packages-select mentorpi_pc_cpp_demo
    │
    ├─> gui (C# 镜像)
    │     └─> build: services/gui/
    │           └─> dotnet publish
    │
    └─> rosbridge (镜像)
          └─> apt install ros-humble-rosbridge-server
```

**共享 base 镜像**：`mentorpi_pc/ros2-base`（一次 build，缓存复用）

## 数据流示例

```
Raspberry Pi
    │
    │ /scan (sensor_msgs/LaserScan)
    │ /odom (nav_msgs/Odometry)
    │ /cmd_vel (geometry_msgs/Twist)
    ▼
┌─────────────┐
│ ROS2 DDS    │ ──> monitor 容器（订阅，打印）
│ (host 网络) │ ──> cpp_demo 容器（订阅 /odom）
│             │ ──> rosbridge 容器（转 WebSocket）
└─────────────┘                │
                               ▼
                          gui 容器（C#，订阅/发布）
```

## 未来扩展

### 1. SLAM 算法模块

```yaml
slam:
  build:
    context: .
    dockerfile: modules/slam/Dockerfile
  network_mode: host
  environment: [ROS_DOMAIN_ID=0]
  volumes:
    - ./maps:/workspace/maps        # 地图保存到宿主
```

### 2. 多 GUI 实例

GUI 容器可以多个实例（不同用户/不同场景）——ROS_DOMAIN_ID 不同就行。

### 3. 算法版本切换

每个模块独立 build 镜像 tag：

```bash
docker compose build --build-arg VERSION=v2 monitor
```

### 4. 部署到云

整套搬到云端只需：

```bash
docker compose -f docker-compose.cloud.yml up
```

ROS_DOMAIN_ID + 网络配置即可。