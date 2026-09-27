# 框架使用速查 — "30 秒判断"

> 给后续开发者 + 你自己用：每次想做某事，30 秒找到对应命令。

---

## 我现在想做什么？

### 🔧 改 Python 节点代码

```bash
# 1. VSCode 打开 ~/mentorpi_pc_framework/
# 2. 编辑 modules/<pkg>/<pkg>/<node>.py
# 3. F1 → Tasks → docker compose build (current module)
# 4. F1 → Tasks → docker compose up -d
# 5. F1 → Tasks → docker compose logs (current module)  # 看效果
```

### 🔧 改 C++ 节点代码

同上，但镜像 build 慢（每次 ~10 秒）。

### 🔧 改 launch / 配置

```bash
# 改 launch/*.py
# 然后 F1 → Tasks → docker compose build (current module)
# 注意:colcon build 会重 build,launch 改动不大通常会跑得很快
```

### 🔧 加新算法模块（全新）

见 `docs/QUICKSTART_FOR_DEVS.md`，10 分钟流程。

### 📊 看 PC 端 ROS 数据（不写代码）

```bash
docker exec mentorpi_monitor bash -lc '
  source /opt/ros/humble/setup.bash &&
  source /workspace/install/setup.bash &&
  ros2 topic list && ros2 topic hz /odom'
```

### 📊 看某个容器实时日志

```bash
docker logs -f mentorpi_monitor
# 或 F1 → Tasks → docker compose logs (current module)
```

### 🐛 调试节点（在容器里）

```bash
docker exec -it mentorpi_monitor bash
# 现在进了容器
source /opt/ros/humble/setup.bash
source /workspace/install/setup.bash
ros2 run <pkg> <node> --ros-args -p print_hz:=2.0
# 或 ros2 topic echo /imu
# 或 rqt_graph
```

### 🎨 改 GUI（C#）

```bash
# 1. 改 services/gui/ 下的 .cs / .axaml
# 2. F1 → Tasks → docker compose build mentorpi_pc_gui
# 3. 启 GUI:
xhost +local:docker
docker compose --profile gui up -d mentorpi_pc_gui
```

### ⏸ 停所有

```bash
docker compose down   # 停所有
# 或单独停:docker compose stop mentorpi_monitor
```

### 🔄 重 build 所有镜像（重置用）

```bash
docker compose build --no-cache
```

### 🗑 删除所有容器和镜像

```bash
docker compose down --rmi all
# ⚠️ 不可逆,镜像全删,要重新 build
```

---

## 我要做的"小动作"

| 动作 | 命令 |
|---|---|
| 改一个 Python 函数 | VSCode 改 → F1 → Tasks → build current → 重起 |
| 看 monitor 状态 | F1 → Tasks → logs (current module) |
| 验证树莓派话题收到 | `docker exec mentorpi_monitor bash -lc 'ros2 topic list'` |
| 进容器 shell | F1 → Tasks → exec into module (bash) |
| 重新 build 单个 | F1 → Tasks → docker compose build (current module) |
| 全 build | F1 → Tasks → docker compose build |
| 起 GUI | `xhost +local:docker && docker compose --profile gui up -d mentorpi_pc_gui` |
| 停所有 | F1 → Tasks → docker compose down |

---

## 网络问题排查

| 症状 | 步骤 |
|---|---|
| 收不到树莓派话题 | `ping 192.168.149.1` |
| 树莓派热点没连 | `nmcli device wifi connect HW-9E7168C4 password hiwonder` |
| ROS_DOMAIN_ID 不一致 | 在 `docker-compose.yml` 每个 service 检查 |
| DDS 收不到但 ICMP 通 | 默认 Fast DDS 不通家用 AP,改 `RMW_IMPLEMENTATION=rmw_cyclonedds_cpp` |

---

## 镜像 / 容器生命周期

```
docker compose build    # build 镜像
docker compose up -d    # 起容器
docker compose down     # 停容器（保留镜像）
docker compose down --rmi all  # 停容器 + 删镜像
```