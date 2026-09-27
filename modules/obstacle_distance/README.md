# obstacle_distance — 算法模块示例

订阅 `/scan` 和 `/odom`，算车体前/左/右三个方向 90° 扇形内的最小障碍物距离，每秒打印一次。

## 数据流

```
/scan  (sensor_msgs/LaserScan)        ┐
                                       ├─→ obstacle_node ─→ 打印 + 可选 topic
/odom  (nav_msgs/Odometry)            ┘
```

## 输出格式

```
[INFO] [obstacle_distance]: front=1.23m  left=0.87m  right=2.45m   pts(f/l/r)=45/32/40
[INFO] [obstacle_distance]: front=1.20m  left=0.85m  right=2.40m   pts(f/l/r)=46/33/41
[INFO] [obstacle_distance]: front=0.42m  left=0.85m  right=2.40m   pts(f/l/r)=46/33/41 ⚠️ ALARM:front
```

任一方向距离 < `safe_distance`（默认 0.5m）时打 `⚠️ ALARM:`。

## 启动

```bash
docker compose up -d obstacle_distance
docker logs -f mentorpi_obstacle
```

或加自定义参数：

```yaml
# docker-compose.yml 里
obstacle_distance:
  environment:
    - ROS_DOMAIN_ID=0
  command: >
    bash -c "source /opt/ros/humble/setup.bash &&
             source /workspace/install/setup.bash &&
             ros2 launch obstacle_distance obstacle.launch.py
             print_hz:=2.0 safe_distance:=0.3"
```

## 文件结构

```
obstacle_distance/
├── Dockerfile
├── package.xml
├── setup.py / setup.cfg
├── resource/obstacle_distance       # marker
├── obstacle_distance/
│   ├── __init__.py
│   └── obstacle_node.py              # 算法
└── launch/
    └── obstacle.launch.py
```

## 关键算法

`obstacle_node.py` 核心逻辑：

1. **分扇形**：每个激光点根据角度分到前/左/右三个 90° 扇形
2. **取最小**：每个扇形内取最小距离
3. **报警**：任一扇形 < safe_distance 触发警告

`wrap_pi` 处理 ±π 跨界（激光扫描范围通常是 ±π）。

## 复用为模板

修改 `obstacle_node.py` 的：

- `_on_scan` 订阅回调 → 换成你的算法
- `_print_status` 输出方式 → 可改为 publish 新话题
- `_sectors` 配置 → 改成你的关注区域

## 跟 `_template/` 的区别

| | `_template/` | `obstacle_distance/` |
|---|---|---|
| 内容 | 空骨架（你能直接拷走）| **完整可运行示例**（后续开发者参考） |
| 依赖 | 无 | rclpy + sensor_msgs + nav_msgs |
| 测试 | 不能跑 | **能跑**——build + up 立刻看输出 |

后续开发者看 `_template/` 起步，看 `obstacle_distance/` 学完整流程。