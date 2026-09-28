"""在线异步建图 launch。

订阅：/scan_raw /odom /tf /tf_static  →  喂 slam_toolbox async mapper
发布：/map + map→odom TF

用法（容器内 / mentorpi.sh）：
  ros2 launch mentorpi_pc_slam_toolbox online_async.launch.py
"""

from launch import LaunchDescription
from launch_ros.actions import Node
from ament_index_python.packages import get_package_share_directory
import os


def generate_launch_description():
    pkg = get_package_share_directory('mentorpi_pc_slam_toolbox')
    params = os.path.join(pkg, 'config', 'mapper_params_online_async.yaml')

    # slam_toolbox async mapper：
    #   - remap /scan → /scan_raw（树莓派端只有 raw，没 /scan）
    return LaunchDescription([
        Node(
            package='slam_toolbox',
            executable='async_slam_toolbox_node',
            name='slam_toolbox',
            parameters=[params],
            remappings=[
                ('/scan', '/scan_raw'),
            ],
            output='screen',
        ),
    ])
