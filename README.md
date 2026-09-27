# MentorPi PC 上位机框架

> 基于 Docker 的 MentorPi 机器人 PC 端开发框架
> 目标：模块化、可扩展、跨平台
> 2026-09-27

---

## 架构

```
┌─────────────────────────────────────────────────────────┐
│                Docker Compose                            │
│                                                           │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐         │
│  │ monitor     │ │ cpp_demo    │ │ <你的模块>  │         │
│  │ (Python)    │ │ (C++)       │ │ (任意语言)  │         │
│  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘         │
│         │               │               │                │
│  ┌──────┴───────────────┴───────────────┴──────┐         │
│  │  rosbridge  (WebSocket)                     │         │
│  └──────────────────────┬───────────────────────┘         │
│                         │                                  │
│  ┌──────────────────────┴───────────────────────┐         │
│  │  gui  (C# Avalonia 跨平台桌面应用)            │         │
│  └────────────────────────────────────────────────┘         │
│                                                           │
│            ROS_DOMAIN_ID=0 (DDS 组播)                     │
└────────────────────────────┬────────────────────────────┘
                             │ WiFi 192.168.149.x
                ┌────────────┴────────────┐
                │ Raspberry Pi (Humble)   │
                │ + bringup (自动启动)    │
                └─────────────────────────┘
```

---

## 目录结构

```
mentorpi_pc_framework/
├── docker-compose.yml       # 一键起所有
├── .env                     # 全局环境变量（ROS_DOMAIN_ID 等）
├── README.md                # ← 你在这
├── base/
│   └── ros2-humble.Dockerfile    # 公共 ROS2 base 镜像
├── services/                # 长期服务
│   ├── gui/                 # C# Avalonia GUI
│   │   ├── Dockerfile
│   │   ├── Gui.csproj
│   │   ├── Program.cs
│   │   ├── App.axaml(.cs)
│   │   ├── Services/RosService.cs
│   │   ├── ViewModels/MainViewModel.cs
│   │   └── Views/MainWindow.axaml(.cs)
│   └── rosbridge/           # WebSocket 桥
│       └── Dockerfile
├── modules/                 # ROS 功能包（每个一目录）
│   ├── _template/           # 新模块模板
│   ├── mentorpi_pc_monitor/ # Python 监控节点
│   ├── mentorpi_pc_cpp_demo/# C++ 演示
│   └── mentorpi_description/# URDF + meshes + launch
└── docs/                    # 详细文档
```

---

## 快速开始

### 1. build base 镜像（一次性）

```bash
cd ~/mentorpi_pc_framework
docker build -t mentorpi_pc/ros2-base:latest -f base/ros2-humble.Dockerfile .
```

### 2. build 所有模块

```bash
docker compose build
```

### 3. 起后台服务

```bash
# 起 ROS 模块 + rosbridge（GUI 默认不跑，要起显式指定）
docker compose up -d

# 看状态
docker compose ps

# 看日志
docker compose logs -f monitor
```

### 4. 起 GUI（需要图形显示）

```bash
# 让 docker 容器用宿主 X11
xhost +local:docker

# 起 GUI 容器
docker compose --profile gui up -d gui
```

---

## 日常使用

| 任务 | 命令 |
|---|---|
| 看 PC 端所有 ROS 节点 | `docker compose ps` |
| 看 monitor 日志 | `docker compose logs -f monitor` |
| 进容器调试 | `docker exec -it mentorpi_monitor bash` |
| 重启某个模块 | `docker compose restart monitor` |
| 停所有模块 | `docker compose down` |
| 重新 build 一个模块 | `docker compose build monitor` |

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
docker compose up my_cool_algo
```

---

## 关键设计

### 每个模块独立容器
- 一个模块挂掉不影响其他
- 模块可以独立 build / 升级 / 重启
- 模块可以用任意语言（Python / C++ / C# / Rust）

### 网络模式 = host
- 所有容器用宿主网络栈
- 直接跟树莓派 `192.168.149.1` DDS 组播通
- 不需要 docker network 配置

### ROS_DOMAIN_ID 统一
- 所有容器 + 树莓派 = 0
- 在 `docker-compose.yml` 的每个 service 里设

### 跨平台 GUI
- 用 **Avalonia**（类似 WPF，跨 Linux/Windows/Mac）
- 用 **RosBridgeNet** 通过 WebSocket 跟 ROS2 通讯
- 后续使用者不用懂 ROS2 native API

---

## 跨 distro 通讯

- PC docker 容器：`ROS2 Humble`（跟树莓派一致）
- 宿主 Jazzy：用 `ROS_DOMAIN_ID=0` + 组播也能通讯（实测过）
- Windows GUI：通过 rosbridge WebSocket，不用 ROS2 native

---

## 环境要求

| 项 | 状态 |
|---|---|
| Docker Engine 29+ | ✅ |
| Docker Compose v2 | ✅ |
| .NET 8 SDK（host）| ✅ |
| X11 (host) | ✅ |
| WiFi 在 192.168.149.x | ✅（连树莓派热点 HW-9E7168C4）|

---

## 文档

- `docs/QUICKSTART_REBOOT.md` —— ⭐ 每次重启后如何快速开始、编译运行
- `docs/SETUP_FROM_ZERO.md` —— Ubuntu 24.04 从 0 搭建整套环境
- `docs/MULTI_PC_COLLAB.md` —— 多台 PC 开发不同功能如何整合
- `docs/ARCHITECTURE.md` —— 详细架构
- `docs/QUICKSTART_FOR_DEVS.md` —— 5 分钟接入新算法模块
- `docs/DECISION_TREE.md` —— 30 秒速查「我现在想做什么」
- `docs/VERIFY.md` —— 树莓派回来后端到端验证 checklist
- `modules/_template/README.md` —— 新模块模板说明
- `services/gui/README.md` —— GUI 开发指南