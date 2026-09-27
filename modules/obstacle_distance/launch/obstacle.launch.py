"""启动 obstacle_distance 算法节点。

参数:
- print_hz: 打印频率 Hz (默认 1.0)
- safe_distance: 报警阈值 米 (默认 0.5)
- scan_topic: 默认 /scan
- odom_topic: 默认 /odom
"""
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description() -> LaunchDescription:
    return LaunchDescription([
        DeclareLaunchArgument('print_hz', default_value='1.0',
                              description='打印频率 Hz'),
        DeclareLaunchArgument('safe_distance', default_value='0.5',
                              description='报警阈值 米'),
        DeclareLaunchArgument('scan_topic', default_value='/scan',
                              description='激光话题'),
        DeclareLaunchArgument('odom_topic', default_value='/odom',
                              description='里程计话题'),

        Node(
            package='obstacle_distance',
            executable='obstacle_node',
            name='obstacle_distance',
            output='screen',
            parameters=[{
                'print_hz': LaunchConfiguration('print_hz'),
                'safe_distance': LaunchConfiguration('safe_distance'),
                'scan_topic': LaunchConfiguration('scan_topic'),
                'odom_topic': LaunchConfiguration('odom_topic'),
            }],
        ),
    ])