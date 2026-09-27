# 多 PC 协作开发整合

> 场景：多个同学 / 多台电脑，各自开发不同功能，最后整合成一套。
> 核心：**树莓派是唯一数据源，每台 PC 只跑自己负责的模块，代码用 GitHub 合并。**

---

## 1. 整体拓扑

```
                    ┌─────────────────────────────┐
                    │  树莓派 5 (192.168.149.1)    │
                    │  MentorPi 容器 → /odom /imu  │
                    │  /battery /scan ... (发布)   │
                    └──────────────┬──────────────┘
                                   │ WiFi 热点 HW-9E7168C4
                                   │ ROS_DOMAIN_ID=0 (DDS 组播)
          ┌──────────────┬─────────┴─────────┬──────────────┐
          ▼              ▼                   ▼              ▼
     ┌─────────┐    ┌─────────┐         ┌─────────┐    ┌─────────┐
     │ PC-A    │    │ PC-B    │         │ PC-C    │    │ PC-D    │
     │ SLAM    │    │ 视觉跟随 │         │ GUI     │    │ 遥控    │
     │ (模块A) │    │ (模块B) │         │ (模块C) │    │ (模块D) │
     └─────────┘    └─────────┘         └─────────┘    └─────────┘
```

**只要所有机器连同一个热点、`ROS_DOMAIN_ID` 都是 0，DDS 组播就会自动互相发现**——PC 之间、PC 和树莓派之间的话题直接互通，不需要额外配置。

---

## 2. 每台 PC 的分工方式

每台 PC 都 clone 同一份仓库，但**只 build / 只跑自己负责的那个模块**：

```bash
cd ~/mentorpi_pc_framework

# 假设我负责 obstacle_distance 这个模块
docker compose build obstacle_distance
docker compose up -d obstacle_distance
```

数据从树莓派来（`/odom` `/scan` 等），我的模块订阅处理、发布自己的结果话题（比如 `/obstacle/dist`），**别台 PC 也能订阅到我的结果**——这就是 DDS 组播的好处。

### 建议：一台机器当「常驻主机」

选一台 PC 常驻跑公共基础模块（`monitor` / `rosbridge`），其他 PC 只跑各自算法：

```bash
# 常驻主机
docker compose up -d mentorpi_pc_monitor rosbridge
```

---

## 3. 代码协作流程（GitHub）

### 3.1 目录约定：一个功能 = 一个模块目录

```
modules/
├── _template/              # 新模块模板（拷它开始）
├── mentorpi_pc_monitor/    # 公共：监控
├── obstacle_distance/      # A 同学：障碍物距离
├── my_slam/                # B 同学：SLAM
├── vision_follow/          # C 同学：视觉跟随
└── ...
```

### 3.2 标准工作流

```bash
# 1. 拉最新
git pull origin main

# 2. 开自己的分支
git checkout -b feature/my_slam

# 3. 加自己的模块（拷模板 → 写代码 → 加 docker-compose 段）
cp -r modules/_template modules/my_slam
# ... 写 modules/my_slam/my_slam/my_node.py ...

# 4. 提交 + 推分支
git add modules/my_slam docker-compose.yml
git commit -m "add my_slam module"
git push -u origin feature/my_slam

# 5. 网页开 Pull Request → 合并进 main
```

详细加模块步骤见 `QUICKSTART_FOR_DEVS.md`。

### 3.3 合并冲突最多的两处

1. **`docker-compose.yml`**——每个人都要加自己的 service 段。冲突时：保留所有人的段，只保证 service 名唯一。
2. **`README.md` / docs**——文档改动冲突直接取并集即可。

---

## 4. 把新模块整合进 docker-compose

每个模块在 `docker-compose.yml` 里加一段（参照 `_template` 和现有模块）：

```yaml
  my_slam:
    build:
      context: .
      dockerfile: modules/my_slam/Dockerfile
    image: mentorpi_pc/my_slam:latest
    container_name: mentorpi_my_slam
    network_mode: host            # 关键：用宿主网络，DDS 才能互通
    environment:
      - ROS_DOMAIN_ID=0           # 关键：跟树莓派同域
    restart: unless-stopped
```

两行 `network_mode: host` + `ROS_DOMAIN_ID=0` 是所有模块跨机互通的前提，别漏。

---

## 5. 跨机通讯要点（踩坑速查）

| 要点 | 说明 |
|------|------|
| 同一子网 | 所有 PC + 树莓派连**同一个热点** `HW-9E7168C4` |
| 同 ROS_DOMAIN_ID | 全部 `0`，任何一个不一致就互相看不见 |
| `network_mode: host` | 容器必须用宿主网络，否则 DDS 组播不通 |
| 订阅 QoS | 树莓派发布是 `reliable`，订阅端用 `qos_profile_system_default`（reliable）|
| 数据源唯一 | 树莓派负责发传感器数据，PC 之间别各自发一份 /odom 打架 |

### 快速验证两台 PC 通不通

PC-A 发个测试话题，PC-B 订阅：

```bash
# PC-A（发布）
docker exec <容器> bash -lc 'source /opt/ros/humble/setup.bash && ros2 topic pub -r 5 /ping_test std_msgs/msg/String "data: hello"'

# PC-B（订阅）
docker exec <容器> bash -lc 'source /opt/ros/humble/setup.bash && ros2 topic echo /ping_test'
```

B 能收到 = 跨机 DDS 通了。

---

## 6. 发布镜像给别人（不想让别台 PC 重新 build）

把自己的模块镜像打包给别人，别台 PC 不用 build 源码，直接 pull 跑：

```bash
# 自己机器上
docker tag mentorpi_pc/my_slam:latest <你的账号>/mentorpi_pc_my_slam:latest
docker push <你的账号>/mentorpi_pc_my_slam:latest

# 别人机器上，docker-compose.yml 里直接写 image（不要 build 段）：
#   my_slam:
#     image: <你的账号>/mentorpi_pc_my_slam:latest
#     network_mode: host
#     environment: [ROS_DOMAIN_ID=0]
docker compose up -d my_slam
```

---

## 7. 一行总结

```
同热点 + 同 ROS_DOMAIN_ID=0 + host 网络 → 各 PC 各跑各模块 → 数据靠 DDS 互通 → 代码靠 GitHub 合并
```
