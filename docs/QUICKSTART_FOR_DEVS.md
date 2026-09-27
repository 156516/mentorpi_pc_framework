# 5 分钟接入新算法模块

> 给后续开发者的快速教程
> 假设：你已经 clone 了 `~/mentorpi_pc_framework/`，树莓派已上电跑 bringup

---

## 场景

你想给 MentorPi 加一个新功能（例如路径规划、视觉跟随、手势控制）。整个流程 5 分钟。

## 前置

```bash
# 1. PC 上连树莓派热点
nmcli device wifi connect HW-9E7168C4 password hiwonder
ping 192.168.149.1  # 验证通

# 2. 进项目目录
cd ~/mentorpi_pc_framework

# 3. 确认基线镜像和 ROS 模块跑着
docker compose ps
# 期望:mentorpi_monitor / mentorpi_cpp_demo / mentorpi_rosbridge 都在 Up

# 4. (可选) GUI 也起
xhost +local:docker
docker compose --profile gui up -d mentorpi_pc_gui
```

## 加新模块

### 1. 拷模板

```bash
cp -r modules/_template modules/my_cool_algo
```

### 2. 改名

```bash
cd modules/my_cool_algo

# 重命名嵌套目录
mv my_cool_algo __tmp_pkg__
mv __tmp_pkg__/my_cool_algo . 2>/dev/null
rm -rf __tmp_pkg__

# 改名 resource 文件
mv resource/_template resource/my_cool_algo
```

### 3. 改 package.xml / setup.py / setup.cfg

```bash
# 批量替换包名（my_cool_algo 改成你的）
sed -i 's/my_cool_algo/你的包名/g' package.xml setup.py setup.cfg
mv resource/my_cool_algo resource/你的包名
```

### 4. 写节点代码

`modules/你的包名/你的包名/my_node.py`:

```python
import rclpy
from rclpy.node import Node

class MyNode(Node):
    def __init__(self):
        super().__init__('my_node')
        self.get_logger().info('my node up')

def main(args=None):
    rclpy.init(args=args)
    rclpy.spin(MyNode())
    rclpy.shutdown()

if __name__ == '__main__':
    main()
```

### 5. 注册到 setup.py 的 entry_points

```python
entry_points={
    'console_scripts': [
        'my_node = 你的包名.my_node:main',
    ],
},
```

### 6. 加进 docker-compose.yml

```yaml
  你的包名:
    build:
      context: .
      dockerfile: modules/你的包名/Dockerfile
    image: mentorpi_pc/你的包名:latest
    container_name: mentorpi_你的包名
    network_mode: host
    environment:
      - ROS_DOMAIN_ID=0
```

### 7. build + 启动

```bash
cd ~/mentorpi_pc_framework
docker compose build 你的包名
docker compose up -d 你的包名

# 看日志
docker logs -f mentorpi_你的包名
```

---

## 接入已有话题（订阅/发布）

`/odom /imu /scan /cmd_vel` 等基础话题直接订阅。HiWonder 私有的 `/ros_robot_controller/*` 话题也可以订阅。

### 常用话题清单

| 话题 | 类型 | 用途 |
|------|------|------|
| `/odom` | `nav_msgs/Odometry` | 里程计（位置 + 速度）|
| `/imu` | `sensor_msgs/Imu` | IMU 姿态（orientation 四元数 + 角速度 + 线加速度）|
| `/scan` | `sensor_msgs/LaserScan` | 2D 激光（树莓派没接 LIDAR 时为空）|
| `/cmd_vel` | `geometry_msgs/Twist` | 控制车 |
| `/ros_robot_controller/battery` | `sensor_msgs/BatteryState` | 电池电压 |
| `/ros_robot_controller/imu_raw` | `sensor_msgs/Imu` | 原始 IMU |
| `/ros_robot_controller/set_motor` | service | 设置电机（一般不用）|

### 订阅示例

```python
from sensor_msgs.msg import Imu

class MyNode(Node):
    def __init__(self):
        super().__init__('my_node')
        self.create_subscription(Imu, '/imu', self.on_imu, 10)

    def on_imu(self, msg):
        # msg.orientation.x/y/z/w 四元数
        # msg.angular_velocity.x/y/z 角速度
        # msg.linear_acceleration.x/y/z 线加速度
        pass
```

### 发布示例

```python
from geometry_msgs.msg import Twist

class MyNode(Node):
    def __init__(self):
        super().__init__('my_node')
        self.cmd_pub = self.create_publisher(Twist, '/cmd_vel', 10)
        self.timer = self.create_timer(0.1, self.send_cmd)

    def send_cmd(self):
        msg = Twist()
        msg.linear.x = 0.1   # 前进 0.1 m/s
        self.cmd_pub.publish(msg)
```

---

## 调试技巧

### 进容器交互

```bash
docker exec -it mentorpi_你的包名 bash
# 现在你在容器里
source /opt/ros/humble/setup.bash
source /workspace/install/setup.bash

# 看话题
ros2 topic list
ros2 topic hz /odom
ros2 topic echo /imu

# 看节点图
ros2 run rqt_graph rqt_graph
```

### 在容器里手动跑节点

```bash
docker exec -it mentorpi_你的包名 bash
source /opt/ros/humble/setup.bash
source /workspace/install/setup.bash
ros2 run 你的包名 my_node --ros-args -p print_hz:=2.0
```

### 看 PC 端 DDS 是否通

```bash
# 任意 ROS 节点 / 容器 / 树莓派都在 ROS_DOMAIN_ID=0 就能互通
ros2 topic list   # 应该看到所有话题
```

---

## 升级模块流程

```bash
# 1. 改代码（VSCode 打开 modules/你的包名/...）

# 2. 重新 build
cd ~/mentorpi_pc_framework
docker compose build 你的包名

# 3. 重启容器（让新镜像生效）
docker compose up -d 你的包名   # 如果 image 名/标签变了,会重建容器

# 或强制:
docker compose up -d --force-recreate 你的包名
```

---

## 发布镜像给别人

```bash
# 1. 推送到 docker hub（需要先 docker login）
docker tag mentorpi_pc/你的包名:latest yourname/mentorpi_pc_your_pkg:latest
docker push yourname/mentorpi_pc_your_pkg:latest

# 2. 别人拉下来跑
# 在他们的 docker-compose.yml:
#   你的包名:
#     image: yourname/mentorpi_pc_your_pkg:latest
#     network_mode: host
#     environment: [ROS_DOMAIN_ID=0]
```

---

## 跨主机 / 跨设备

如果你想在**树莓派以外的另一台 PC** 上跑算法节点：

```yaml
# 另一台 PC 的 docker-compose.yml:
services:
  my_cool_algo:
    image: yourname/mentorpi_pc_your_pkg:latest
    network_mode: host
    environment:
      - ROS_DOMAIN_ID=0    # 关键:与 PC + Pi 同域
    volumes:
      - ./modules/my_cool_algo:/workspace/src/my_cool_algo  # 改代码实时生效
```

只要都在 ROS_DOMAIN_ID=0 + 同子网（192.168.149.x），就能跨主机通讯。

---

## 故障排查

| 症状 | 解决 |
|------|------|
| `no module` 跑节点 | `source /workspace/install/setup.bash` |
| `package not found` | 镜像 build 没成功（看 build 日志）|
| 收不到树莓派话题 | `ping 192.168.149.1` 验证网络 |
| `ros2 topic list` 空 | 容器和树莓派 ROS_DOMAIN_ID 不一致 |
| container 启动失败 | `docker logs mentorpi_<pkg>` 看错误 |
| GUI 没数据 | rosbridge 容器没起，或 ROS_BRIDGE_URL 错 |

---

## 一行复盘

```
1. cp -r modules/_template modules/<新包名>
2. 重命名嵌套目录 + resource
3. sed -i 's/my_cool_algo/<新包名>/g' setup.py setup.cfg package.xml
4. 写 <新包名>/<新包名>/<node>.py
5. setup.py 注册 entry_points
6. docker-compose.yml 加一段
7. docker compose build <新包名>
8. docker compose up -d <新包名>
```

10 分钟接入。