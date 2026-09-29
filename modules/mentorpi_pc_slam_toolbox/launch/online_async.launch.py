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

    # slam_toolbox：
    #   - remap /scan → /scan_raw（树莓派端只有 raw，没 /scan）
    #
    # 静态 TF：robot_state_publisher 在树莓派上发的 /tf_static 经常在 PC 端
    # 收不到（TRANSIENT_LOCAL + DDS 抖动），slam_toolbox 找不到 base_link → lidar_frame
    # 就持续 Message Filter dropping message。
    # 这里直接在容器内补发 base_link → lidar_frame 静态变换（URDF 真值）：
    #   lidar 在 base_link 下：xyz=-0.00168 -0.00135 0.087501, rpy=0 0 0
    #   （见 modules/mentorpi_description/urdf/tank.xacro lidar_Joint）
    return LaunchDescription([
        Node(
            package='tf2_ros',
            executable='static_transform_publisher',
            name='base_link_to_lidar_frame',
            arguments=[
                '--x', '-0.00168',
                '--y', '-0.00135',
                '--z', '0.087501',
                '--roll', '0',
                '--pitch', '0',
                '--yaw', '0',
                '--frame-id', 'base_link',
                '--child-frame-id', 'lidar_frame',
            ],
            output='screen',
        ),

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