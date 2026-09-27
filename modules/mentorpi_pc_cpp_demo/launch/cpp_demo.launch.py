"""启动 C++ 演示节点。

默认只跑 odom_echo_node（只读）。需要测发布 /cmd_vel 时手动开 odom_pub_node。
"""
from launch import LaunchDescription, conditions
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description() -> LaunchDescription:
    enable_odom_pub = LaunchConfiguration('enable_odom_pub')

    return LaunchDescription([
        DeclareLaunchArgument(
            'enable_odom_pub', default_value='false',
            description='是否同时启动 odom_pub_node（演示发布 /cmd_vel）'),

        Node(
            package='mentorpi_pc_cpp_demo',
            executable='odom_echo_node',
            name='odom_echo_node',
            output='screen',
            parameters=[{'print_hz': 1.0}],
        ),

        Node(
            package='mentorpi_pc_cpp_demo',
            executable='odom_pub_node',
            name='odom_pub_node',
            output='screen',
            parameters=[{
                'linear_x': 0.0,
                'angular_z': 0.0,
                'publish_hz': 0.0,  # 0 = 不开定时器
            }],
            condition=conditions.IfCondition(enable_odom_pub),
        ),
    ])