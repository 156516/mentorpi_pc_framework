# Ubuntu 24.04 从 0 搭建整套环境

> 适用：一台**全新的 Ubuntu 24.04 主机**（PC 上位机），从零搭起 MentorPi 模块化开发框架。
> 目标：一次配好，之后用 `QUICKSTART_REBOOT.md` 日常开发。
> 前置：已有一台树莓派 5 + HiWonder MentorPi（出厂带 ROS2，能发话题）。

---

## 0. 环境总览

```
PC (Ubuntu 24.04 宿主)
├── Docker Engine + Compose v2
├── .NET 8 SDK（host，可选：本地验证 GUI 编译用）
└── ~/mentorpi_pc_framework/    ← 本项目
      ├── docker-compose.yml    ← 一键起 monitor/cpp_demo/obstacle/rosbridge/gui
      ├── base/                 ← 公共 ROS2 Humble 镜像
      ├── modules/              ← 各功能模块（独立容器）
      ├── services/             ← rosbridge + C# GUI
      └── docs/                 ← 文档
```

ROS2 跑在 **Docker 容器**里（Humble，跟树莓派一致），宿主 Ubuntu 24.04 不用装 ROS2。

---

## 1. 装 Docker

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg

# 阿里云 Docker 镜像源（避开校园网劫持）
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://mirrors.aliyun.com/docker-ce/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://mirrors.aliyun.com/docker-ce/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# 免 sudo 用 docker
sudo usermod -aG docker $USER
newgrp docker

docker --version && docker compose version   # 验证
```

> 如果 `docker` 拉镜像慢，配镜像加速器（`/etc/docker/daemon.json`）：
> `{"registry-mirrors":["https://docker.m.daocloud.io"]}` 然后 `sudo systemctl restart docker`。

---

## 2. 装 .NET 8 SDK（可选，仅 GUI 本地编译需要）

GUI 已经封装进 Docker（`docker compose --profile gui build` 会自动在容器里装 .NET）。**只有想在本机直接 `dotnet build` 调试 C# 时才需要装**：

```bash
# 微软官方脚本（走 dot.net，一般能连）
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
dotnet --version
```

---

## 3. 拿代码

```bash
cd ~
git clone git@github.com:<你的账号>/mentorpi_pc_framework.git
cd mentorpi_pc_framework
```

---

## 4. build base 镜像（一次性，~5 分钟）

```bash
cd ~/mentorpi_pc_framework
docker build -t mentorpi_pc/ros2-base:latest -f base/ros2-humble.Dockerfile .
```

这个镜像基于 `osrf/ros:humble-desktop`，内置了阿里云 apt/PyPI 源 + 常用 ROS2 包，所有模块共享它。

---

## 5. build 所有模块

```bash
docker compose build
```

首次要拉基础镜像 + 下载依赖，会比较久（10~30 分钟，看网速）。

| 模块 | 说明 | 默认启动 |
|------|------|---------|
| `mentorpi_pc_monitor` | Python 监控节点 | ✅ |
| `mentorpi_pc_cpp_demo` | C++ 订阅 /odom | ✅ |
| `obstacle_distance` | 算法示例 | ✅ |
| `rosbridge` | WebSocket 桥 | ✅ |
| `mentorpi_description` | URDF/RViz | ❌ (tools profile) |
| `mentorpi_pc_gui` | C# 控制台 | ❌ (gui profile) |

---

## 6. 启动 + 验证

```bash
docker compose up -d
docker compose ps                       # 应该看到 4 个 Up

# 起 GUI
xhost +local:docker
bash start_gui.sh
```

然后按 `QUICKSTART_REBOOT.md` 的「第 4 步」验证 `/odom` 数据。

---

## 7. 树莓派侧确认

PC 端搭好后，确认树莓派在发话题：

```bash
ping -c 2 192.168.149.1                        # 树莓派在线
ssh pi@192.168.149.1                            # 密码 pi

# 在树莓派上：
docker exec MentorPi bash -lc 'ros2 node list' | grep -i robot_controller
# 应该有 ros_robot_controller（串口驱动）→ 说明 STM32 正常
```

树莓派是 HiWonder 出厂的，`start_node.sh` 会自动起 bringup，一般不用动。

---

## 附录 A：搭建时最常踩的坑

| 坑 | 现象 | 解法 |
|----|------|------|
| 校园网 DNS 劫持 | apt/pip/nuget 下载卡死或跳登录页 | 全换国内源（阿里云 apt/pip、nuget.azure.cn、docker 加速器）|
| NuGet 源 | `nuget.cdn.azure.cn` 解析不了 | 用 `nuget.azure.cn/v3/index.json` |
| GUI build 报 NETSDK1064 | 本地 build 的 bin/obj 被 COPY 进镜像，覆盖了 restore 的 assets | 项目里已有 `.dockerignore` 排除 bin/obj，别删 |
| GUI 起不来 DllNotFound libfontconfig | runtime 镜像缺原生库/中文字体 | 已内建在 GUI Dockerfile，别删那段 apt install |
| 数据全 0 | QoS best_effort 收不到 reliable 发布 | 订阅统一用 `qos_profile_system_default`（已默认）|

---

## 附录 B：树莓派串口设备名问题（/dev/rrc）

树莓派的 STM32 驱动默认找 **`/dev/rrc`**（由 udev 规则把 `ttyACM*` 映射而来）。

**症状**：驱动起不来，`ros2 run ros_robot_controller ros_robot_controller` 报
`could not open port /dev/rrc: No such file or directory`。

**原因**：STM32 被识别成了 `ttyUSB*`（而非 `ttyACM*`），udev 规则没匹配，`/dev/rrc` 没生成。

**临时救急**（在树莓派上）：
```bash
docker exec -u root MentorPi bash -lc 'ln -sf /dev/ttyUSB0 /dev/rrc'
bash ~/mentorpi/start_node.sh
```

**根治**：改 udev 规则让它也匹配 `ttyUSB*`，或让 STM32 每次以 ttyACM 枚举（重新上电、插对 USB 口）。

> 判据：树莓派正常启动会「滴」一声（STM32 自检）。没滴声 = STM32 没起来，先去解决树莓派上电/串口。
