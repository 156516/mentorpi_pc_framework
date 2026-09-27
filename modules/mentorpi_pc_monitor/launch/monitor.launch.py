"""
启动监控节点 + （可选）仪表板。

用法：
    ros2 launch mentorpi_pc_monitor monitor.launch.py dashboard:=true
    ros2 launch mentorpi_pc_monitor monitor.launch.py           # 仅控制台监控
"""

from launch import LaunchDescription, conditions
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description() -> LaunchDescription:
    dashboard = LaunchConfiguration('dashboard')
    print_hz = LaunchConfiguration('print_hz')
    cmd_topic = LaunchConfiguration('cmd_topic')

    return LaunchDescription([
        DeclareLaunchArgument(
            'dashboard', default_value='false',
            description='是否同时打开 PyQt5 仪表板'),
        DeclareLaunchArgument(
            'print_hz', default_value='1.0',
            description='终端状态摘要打印频率 Hz'),
        DeclareLaunchArgument(
            'cmd_topic', default_value='/app/cmd_vel',
            description='订阅的 cmd_vel 话题名'),

        Node(
            package='mentorpi_pc_monitor',
            executable='monitor_node',
            name='monitor_node',
            output='screen',
            parameters=[{
                'print_hz': print_hz,
                'cmd_topic': cmd_topic,
            }],
        ),

        # 仪表板（条件执行）
        Node(
            package='mentorpi_pc_monitor',
            executable='dashboard_node',
            name='dashboard_node',
            output='screen',
            condition=conditions.IfCondition(dashboard),
            parameters=[{
                'cmd_topic': cmd_topic,
            }],
        ),
    ])