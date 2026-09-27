# 新算法模块模板

10 分钟接入 MentorPi 框架：

## 步骤

```bash
# 1. 拷贝模板（改成你的包名）
cp -r modules/_template modules/my_cool_algo

# 2. 改名（_template → my_cool_algo）
cd modules/my_cool_algo
mv _template __init__.py my_cool_algo/
mv resource/_template resource/my_cool_algo

# 3. 改 package.xml / setup.py / setup.cfg 里的 my_cool_algo
# （用 sed 批量替换更快）
sed -i 's/my_cool_algo/你的包名/g' package.xml setup.py setup.cfg
mv resource/my_cool_algo resource/你的包名

# 4. 写节点代码
# modules/my_cool_algo/my_cool_algo/my_node.py

# 5. 注册到 setup.py 的 entry_points
# 'my_node = my_cool_algo.my_node:main',

# 6. 在 docker-compose.yml 加一段：
#   my_cool_algo:
#     build: ./modules/my_cool_algo
#     network_mode: host
#     environment: [ROS_DOMAIN_ID=0]

# 7. 启动
cd ~/mentorpi_pc_framework
docker compose up my_cool_algo
```

## 容器内的 ROS 环境

- **基础镜像**：`mentorpi_pc/ros2-base`（已经在 base/ros2-humble.Dockerfile 定义）
- **ROS_DOMAIN_ID=0** — 与树莓派、其他模块在同一 DDS 域
- **build 命令**：`source /opt/ros/humble/setup.bash && colcon build --symlink-install`
- **运行命令**：`ros2 run <pkg> <exec>`

## 调试

```bash
# 进入正在跑的容器
docker exec -it my_cool_algo bash

# 查看日志
docker logs my_cool_algo

# 看 ROS 话题
docker exec my_cool_algo bash -c "source /opt/ros/humble/setup.bash && ros2 topic list"
```

## 数据流

模块订阅 / 发布的话题在 README 里写明。例如：

```yaml
# 订阅
- /scan          # 来自 LIDAR
- /odom          # 来自里程计

# 发布
- /cmd_vel       # 控制车
- /planned_path # 路径
```

## 测试

不需要写测试，但要保证 `docker compose up` 后能在 PC 端 `ros2 topic list` 看到你的话题。