# 完整示例：加新功能（C++ / Python / GUI）

> 给后续开发者的**端到端示例**。每个例子都从 0 走到能在 GUI 看到新数据。
> 三个场景：
> 1. **Python ROS 节点**：订阅 /scan 发 /obstacle_warning（10 分钟）
> 2. **C++ ROS 节点**：订阅 /odom 发 /distance_traveled（10 分钟）
> 3. **GUI 显示新话题**：把上面两个节点的话题都显示出来（5 分钟）

---

## 场景 1：Python 新 ROS 节点 —— /scan → /obstacle_warning

**目标**：订阅激光雷达 `/scan`，算最近障碍物距离，发布到 `/obstacle_warning`（std_msgs/Float32）。同时在控制台每 1 秒打印一次。

### 步骤

#### 1. 复制模板

```bash
cd ~/mentorpi_pc_framework/modules
cp -r _template obstacle_warning
```

#### 2. 改包名（sed 批量）

```bash
cd obstacle_warning
# _template → obstacle_warning
sed -i 's/_template/obstacle_warning/g' package.xml setup.py setup.cfg
mv _template obstacle_warning             # 内嵌目录重命名
mv resource/_template resource/obstacle_warning
rm -f obstacle_warning/_template 2>/dev/null  # 删残留空 __init__
mv obstacle_warning/_template obstacle_warning/__init__.py 2>/dev/null
ls -la obstacle_warning/                   # 应该看到 __init__.py
```

#### 3. 写节点代码 `obstacle_warning/obstacle_warning/warning_node.py`

```python
"""
订阅 /scan，算最近障碍物距离（米）→ 发布到 /obstacle_warning。
"""
from __future__ import annotations

import math

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy
from sensor_msgs.msg import LaserScan
from std_msgs.msg import Float32


class WarningNode(Node):
    def __init__(self) -> None:
        super().__init__('obstacle_warning')

        # 参数
        self.declare_parameter('scan_topic', '/scan')
        self.declare_parameter('warning_topic', '/obstacle_warning')
        self.declare_parameter('safe_distance', 0.5)  # < 此值报警

        scan_topic = str(self.get_parameter('scan_topic').value)
        warning_topic = str(self.get_parameter('warning_topic').value)
        self._safe = float(self.get_parameter('safe_distance').value)

        # HiWonder 节点 RELIABLE，订阅端必须显式 RELIABLE
        reliable_qos = QoSProfile(
            reliability=ReliabilityPolicy.RELIABLE, depth=10)

        self._scan_sub = self.create_subscription(
            LaserScan, scan_topic, self._on_scan, reliable_qos)
        self._warning_pub = self.create_publisher(
            Float32, warning_topic, reliable_qos)

        self._latest_min = float('inf')
        self.get_logger().info(
            f'obstacle_warning up — scan={scan_topic} → {warning_topic}')

    def _on_scan(self, msg: LaserScan) -> None:
        # 找所有有效读数的最小值
        valid = [r for r in msg.ranges
                 if not math.isinf(r) and not math.isnan(r)
                 and msg.range_min <= r <= msg.range_max]
        self._latest_min = min(valid) if valid else float('inf')

    def _print_and_publish(self) -> None:
        if math.isfinite(self._latest_min):
            self._warning_pub.publish(Float32(data=self._latest_min))
            level = 'ALARM' if self._latest_min < self._safe else 'ok'
            self.get_logger().info(
                f'min_dist={self._latest_min:.2f}m  [{level}]')
        else:
            self.get_logger().info('waiting for /scan...')

    def start(self) -> None:
        # 1 Hz 定时打印 + 发布
        self.create_timer(1.0, self._print_and_publish)


def main(args=None) -> None:
    rclpy.init(args=args)
    node = WarningNode()
    node.start()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
```

#### 4. 注册 entry_point（`setup.py` 里）

```python
entry_points={
    'console_scripts': [
        'warning_node = obstacle_warning.warning_node:main',
    ],
},
```

#### 5. 加 launch 文件 `launch/warning.launch.py`

```python
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description() -> LaunchDescription:
    return LaunchDescription([
        DeclareLaunchArgument('safe_distance', default_value='0.5'),
        Node(
            package='obstacle_warning',
            executable='warning_node',
            name='obstacle_warning',
            output='screen',
            parameters=[{
                'safe_distance': LaunchConfiguration('safe_distance'),
            }],
        ),
    ])
```

#### 6. 加进 `docker-compose.yml`（项目根目录）

```yaml
  obstacle_warning:
    build:
      context: .
      dockerfile: modules/obstacle_warning/Dockerfile
    image: mentorpi_pc/obstacle_warning:latest
    container_name: mentorpi_obstacle_warning
    network_mode: host
    environment:
      - ROS_DOMAIN_ID=0
    restart: unless-stopped
```

#### 7. build + run + 验证

```bash
# 在项目根目录
bash mentorpi.sh rebuild -w obstacle_warning

# 跟日志
bash mentorpi.sh logs obstacle_warning
# 期望：每 1 秒一行 min_dist=... ok / ALARM

# 在 monitor 容器里测话题频率
docker exec mentorpi_monitor bash -lc \
  'source /opt/ros/humble/setup.bash && \
   source /workspace/install/setup.bash && \
   ros2 topic hz /obstacle_warning'
```

---

## 场景 2：C++ 新 ROS 节点 —— /odom → /distance_traveled

**目标**：订阅 `/odom`，累计行驶距离（米）→ 发布到 `/distance_traveled`（std_msgs/Float32）。

### 步骤

#### 1. 复制 cpp_demo 模板

```bash
cd ~/mentorpi_pc_framework/modules
cp -r mentorpi_pc_cpp_demo distance_tracker
```

#### 2. 改包名

```bash
cd distance_tracker
sed -i 's/mentorpi_pc_cpp_demo/distance_tracker/g' CMakeLists.txt package.xml

# 文件改名
mv src/odom_echo_node.cpp src/distance_node.cpp
mv src/odom_pub_node.cpp src/odom_helper.cpp
# odom_echo_lib.cpp 保留，但改成距离计算
mv src/odom_echo_lib.cpp src/distance_lib.cpp
mv include/mentorpi_pc_cpp_demo include/distance_tracker
# 清空旧的
rm -rf src/odom_echo_node.cpp src/odom_pub_node.cpp src/odom_echo_lib.cpp
```

#### 3. CMakeLists.txt

关键改动：

```cmake
project(distance_tracker LANGUAGES CXX)

# ...find_package 一致...

# 公共 lib（距离增量计算）
add_library(distance_lib SHARED
  src/distance_lib.cpp
)
target_include_directories(distance_lib PUBLIC
  $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
)
ament_target_dependencies(distance_lib PUBLIC
  "rclcpp" "nav_msgs"
)

# 节点
add_executable(distance_node src/distance_node.cpp)
target_link_libraries(distance_node distance_lib)
ament_target_dependencies(distance_node "rclcpp" "nav_msgs" "std_msgs")

install(TARGETS
  distance_node
  distance_lib
  RUNTIME DESTINATION lib/${PROJECT_NAME}
  ARCHIVE DESTINATION lib/${PROJECT_NAME}
  LIBRARY DESTINATION lib/${PROJECT_NAME}
)
install(DIRECTORY launch/
  DESTINATION share/${PROJECT_NAME}/launch
  OPTIONAL
)

ament_package()
```

#### 4. 头文件 `include/distance_tracker/distance_lib.hpp`

```cpp
#pragma once
#include <cmath>
#include <nav_msgs/msg/odometry.hpp>

namespace distance_tracker {

// 算两帧之间的位移距离（米）
inline double step_distance(const nav_msgs::msg::Odometry& prev,
                            const nav_msgs::msg::Odometry& cur)
{
    const auto& a = prev.pose.pose.position;
    const auto& b = cur.pose.pose.position;
    return std::hypot(b.x - a.x, b.y - a.y);
}

}  // namespace distance_tracker
```

#### 5. 库实现 `src/distance_lib.cpp`（占位）

```cpp
#include "distance_tracker/distance_lib.hpp"

namespace distance_tracker {
// 头文件全是 inline，这里仅占位以让 SHARED 库 link 通。
int dummy_keep_linker_happy = 0;
}
```

#### 6. 节点 `src/distance_node.cpp`

```cpp
// 订阅 /odom，累计行驶距离（米）→ 发布 /distance_traveled
#include <chrono>
#include <memory>

#include "rclcpp/rclcpp.hpp"
#include "nav_msgs/msg/odometry.hpp"
#include "std_msgs/msg/float32.hpp"
#include "distance_tracker/distance_lib.hpp"

using namespace std::chrono_literals;

class DistanceNode : public rclcpp::Node {
public:
    DistanceNode() : Node("distance_node")
    {
        this->declare_parameter<std::string>("odom_topic", "/odom");
        this->declare_parameter<std::string>("dist_topic", "/distance_traveled");

        // HiWonder RELIABLE → 订阅端必须显式 RELIABLE
        auto reliable_qos = rclcpp::QoS(rclcpp::KeepLast(10))
            .reliability(rclcpp::ReliabilityPolicy::Reliable);

        odom_sub_ = this->create_subscription<nav_msgs::msg::Odometry>(
            "/odom", reliable_qos,
            [this](const nav_msgs::msg::Odometry::SharedPtr msg) {
                if (have_prev_) {
                    total_ += distance_tracker::step_distance(prev_odom_, *msg);
                }
                prev_odom_ = *msg;
                have_prev_ = true;

                auto out = std_msgs::msg::Float32();
                out.data = total_;
                dist_pub_->publish(out);
            });

        dist_pub_ = this->create_publisher<std_msgs::msg::Float32>(
            "/distance_traveled", reliable_qos);

        RCLCPP_INFO(this->get_logger(),
            "distance_node up — total=%.3fm", total_);
    }

private:
    rclcpp::Subscription<nav_msgs::msg::Odometry>::SharedPtr odom_sub_;
    rclcpp::Publisher<std_msgs::msg::Float32>::SharedPtr dist_pub_;

    bool have_prev_ = false;
    nav_msgs::msg::Odometry prev_odom_;
    double total_ = 0.0;
};

int main(int argc, char * argv[])
{
    rclcpp::init(argc, argv);
    rclcpp::spin(std::make_shared<DistanceNode>());
    rclcpp::shutdown();
    return 0;
}
```

#### 7. 加 launch `launch/distance.launch.py`

```python
from launch import LaunchDescription
from launch_ros.actions import Node


def generate_launch_description() -> LaunchDescription:
    return LaunchDescription([
        Node(
            package='distance_tracker',
            executable='distance_node',
            name='distance_node',
            output='screen',
        ),
    ])
```

#### 8. Dockerfile

```dockerfile
FROM mentorpi_pc/ros2-base:latest

WORKDIR /workspace/src/distance_tracker
COPY . /workspace/src/distance_tracker/

RUN bash -c "source /opt/ros/humble/setup.bash && cd /workspace \
 && colcon build --symlink-install --packages-select distance_tracker --cmake-args -DCMAKE_BUILD_TYPE=Release"

CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 launch distance_tracker distance.launch.py"]
```

#### 9. 加进 `docker-compose.yml`

```yaml
  distance_tracker:
    build:
      context: .
      dockerfile: modules/distance_tracker/Dockerfile
    image: mentorpi_pc/distance_tracker:latest
    container_name: mentorpi_distance
    network_mode: host
    environment:
      - ROS_DOMAIN_ID=0
    restart: unless-stopped
```

#### 10. build + run + 验证

```bash
bash mentorpi.sh rebuild -w distance_tracker
bash mentorpi.sh logs distance_tracker
# 推动小车 → 应该看到 total=m 在涨

# 测话题
docker exec mentorpi_monitor bash -lc \
  'source /opt/ros/humble/setup.bash && \
   source /workspace/install/setup.bash && \
   ros2 topic echo /distance_traveled --once'
```

---

## 场景 3：GUI 显示新话题（不改 ROS 节点）

**目标**：让 GUI 显示 `/obstacle_warning`（最近障碍距离）和 `/distance_traveled`（累计里程）。

**前提**：场景 1 / 2 已经跑起来，两个新话题已经在 DDS 上发布了。

**GUI 开发者**只需要改 3 个文件，**完全不用懂 ROS**。

### 1. `services/gui/Messages/RosMessages.cs` —— 加两个 DTO

```csharp
// 已有 BatteryState / Odometry / Imu / Quaternion / ...
// 在文件末尾加：

// std_msgs/msg/Float32 — /obstacle_warning /distance_traveled 用这个
public sealed class Float32Msg
{
    public float Data { get; set; }
}
```

### 2. `services/gui/ViewModels/MainViewModel.cs` —— 加订阅 + 属性

```csharp
// 已有 BatteryVoltage / BatteryPercentage / LinearVel / AngularVel / ImuRoll / ...
// 在 class 里加两个字段：

[ObservableProperty] private float _obstacleDistance;
[ObservableProperty] private float _distanceTraveled;

// 在 SubscribeTopicsAsync() 里加订阅：

await _ros.SubscribeAsync<Float32Msg>(
    "/obstacle_warning", "std_msgs/msg/Float32", msg =>
{
    ObstacleDistance = msg.Data;
});

await _ros.SubscribeAsync<Float32Msg>(
    "/distance_traveled", "std_msgs/msg/Float32", msg =>
{
    DistanceTraveled = msg.Data;
});
```

### 3. `services/gui/Views/MainWindow.axaml` —— 加显示

中间 Grid 里加一列：

```xml
<!-- 已有电池 / 速度 / IMU 三列 -->
<!-- 在 IMU 那列后面加： -->

<!-- 障碍距离 -->
<Border Grid.Column="3" Background="#FAFAFA" Padding="12" CornerRadius="4" Margin="4,0,0,0">
  <StackPanel Spacing="6">
    <TextBlock Text="障碍距离" FontSize="14" Foreground="Gray" />
    <TextBlock Text="{Binding ObstacleDistance, StringFormat='{}{0:F2} m'}" FontSize="20" />
    <TextBlock Foreground="Red"
               Text="{Binding ObstacleDistance, StringFormat='⚠️ 接近'}"
               IsVisible="{Binding ObstacleDistance, Converter={x:Static StringConverters.LessThan}, ConverterParameter=0.5}" />
  </StackPanel>
</Border>

<!-- 累计里程 -->
<Border Grid.Column="4" Background="#FAFAFA" Padding="12" CornerRadius="4" Margin="4,0,0,0">
  <StackPanel Spacing="6">
    <TextBlock Text="累计里程" FontSize="14" Foreground="Gray" />
    <TextBlock Text="{Binding DistanceTraveled, StringFormat='{}{0:F2} m'}" FontSize="20" />
  </StackPanel>
</Border>
```

并把外层 Grid 的 `ColumnDefinitions="*,*,*"` 改成 `ColumnDefinitions="*,*,*,*,*"`。

### 4. 跑

```bash
# 打开 GUI 子项目
code ~/mentorpi_pc_framework/services/gui

# 宿主直接跑（最快反馈）
cd ~/mentorpi_pc_framework/services/gui
dotnet run

# 弹窗 → 点「连接」→ 应该看到「障碍距离」「累计里程」两个新卡片
# 推车 / 靠近墙 → 数字实时更新
```

或者 docker：

```bash
bash mentorpi.sh rebuild -w mentorpi_pc_gui
bash mentorpi.sh gui
```

---

## 三类工作的边界总结

| 任务 | 改的文件 | 谁来改 | 是否需要懂 ROS |
|------|---------|------|------|
| **新 Python 节点** | `modules/<新包>/.../*.py` + `docker-compose.yml` | 后端开发者 | ✅ 需要 |
| **新 C++ 节点** | `modules/<新包>/src/*.cpp` + `include/.../*.hpp` + `CMakeLists.txt` + `docker-compose.yml` | 后端开发者 | ✅ 需要 |
| **GUI 显示新话题** | `services/gui/Messages/*.cs` + `ViewModels/*.cs` + `Views/*.axaml` | 前端开发者 | ❌ 不需要 |
| **改 ROS 通讯底层** | `services/gui/Services/RosService.cs` | 架构师 | ✅ 需要（一般不改）|

---

## 关键约定（容易踩的坑）

1. **QoS**：所有订阅都要 `reliability=ReliabilityPolicy.RELIABLE`
   - Python: `QoSProfile(reliability=ReliabilityPolicy.RELIABLE, depth=10)`
   - C++: `rclcpp::QoS(rclcpp::KeepLast(10)).reliability(rclcpp::ReliabilityPolicy::Reliable)`

2. **包名一致性**：`package.xml` / `setup.py` / 内嵌目录 / `entry_points` 全要同步改，colcon build 才会找到

3. **docker-compose service 名 = `mentorpi.sh rebuild` 参数**：`docker compose build <这里的名字>`

4. **GUI 改完代码看效果**：
   - 宿主：`cd services/gui && dotnet run`
   - 容器：`bash mentorpi.sh rebuild -w mentorpi_pc_gui`

5. **没数据 = 90% 是 QoS / 订阅类型不对**：
   ```bash
   # PC 端直接测话题有没有数据（绕过 rosbridge / GUI）
   docker exec mentorpi_monitor bash -lc \
     'source /opt/ros/humble/setup.bash && \
      source /workspace/install/setup.bash && \
      ros2 topic hz <话题名>'
   ```
   能看到 Hz 但订阅收不到 = 八成 QoS
   啥都没有 = publisher 没起来 / 话题名拼错

---

## 5 分钟跑完一个完整 demo

最快路径（验证整个 pipeline）：

```bash
# 假设你已经跑着 monitor + rosbridge + 树莓派 bringup
cd ~/mentorpi_pc_framework

# 1. 跑场景 1 的 Python 节点（复制上面的 obstacle_warning）
bash mentorpi.sh rebuild -w obstacle_warning
bash mentorpi.sh logs obstacle_warning   # 看 /obstacle_warning 数据

# 2. 改 GUI 显示新话题（场景 3 的步骤 1~3）
code services/gui
# 改 3 个文件（Messages / ViewModels / Views）
cd services/gui && dotnet run
# 弹窗 → 点「连接」→ 看到新卡片

# 3. 总耗时：Python 节点 10 分钟 + GUI 改动 5 分钟 = 15 分钟
```