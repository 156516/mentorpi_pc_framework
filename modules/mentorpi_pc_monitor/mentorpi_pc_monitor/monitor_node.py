"""
PC 端 MentorPi 监控节点（纯控制台版）。

订阅 /ros_robot_controller/battery、/imu、/odom、/cmd_vel，
在终端里定期打印当前状态摘要——确认能收到树莓派的话题，
同时给 PyQt 仪表板打底。

用法：
    ros2 run mentorpi_pc_monitor monitor_node
    # 或带参：ros2 run mentorpi_pc_monitor monitor_node --ros-args -p print_hz:=2.0
"""

from __future__ import annotations

import math
import time
from dataclasses import dataclass, field
from typing import Optional

import rclpy
from rclpy.node import Node
from rclpy.qos import qos_profile_system_default
from sensor_msgs.msg import BatteryState, Imu
from nav_msgs.msg import Odometry
from geometry_msgs.msg import Twist


@dataclass
class RobotState:
    """汇总最近一次收到的传感器值。"""
    battery_voltage: Optional[float] = None
    battery_percentage: Optional[float] = None
    battery_stamp: float = 0.0

    imu_roll: Optional[float] = None
    imu_pitch: Optional[float] = None
    imu_yaw: Optional[float] = None
    imu_stamp: float = 0.0

    odom_x: Optional[float] = None
    odom_y: Optional[float] = None
    odom_yaw: Optional[float] = None
    odom_v: float = 0.0
    odom_w: float = 0.0
    odom_stamp: float = 0.0

    cmd_v: float = 0.0
    cmd_w: float = 0.0
    cmd_stamp: float = 0.0

    last_topic_seen: dict[str, float] = field(default_factory=dict)


def quat_to_rpy(qx: float, qy: float, qz: float, qw: float) -> tuple[float, float, float]:
    """四元数 → 欧拉角 (roll, pitch, yaw)，单位弧度。"""
    # roll (x-axis rotation)
    sinr_cosp = 2.0 * (qw * qx + qy * qz)
    cosr_cosp = 1.0 - 2.0 * (qx * qx + qy * qy)
    roll = math.atan2(sinr_cosp, cosr_cosp)

    # pitch (y-axis rotation)
    sinp = 2.0 * (qw * qy - qz * qx)
    pitch = math.copysign(math.pi / 2.0, sinp) if abs(sinp) >= 1.0 else math.asin(sinp)

    # yaw (z-axis rotation)
    siny_cosp = 2.0 * (qw * qz + qx * qy)
    cosy_cosp = 1.0 - 2.0 * (qy * qy + qz * qz)
    yaw = math.atan2(siny_cosp, cosy_cosp)

    return roll, pitch, yaw


class MonitorNode(Node):
    """订阅车端话题，定期打印状态摘要。"""

    def __init__(self) -> None:
        super().__init__('mentorpi_pc_monitor')
        self.declare_parameter('print_hz', 1.0)
        self.declare_parameter('stale_sec', 3.0)
        self.declare_parameter('cmd_topic', '/app/cmd_vel')

        print_hz = float(self.get_parameter('print_hz').value)
        self._stale_sec = float(self.get_parameter('stale_sec').value)
        cmd_topic = str(self.get_parameter('cmd_topic').value)

        self.state = RobotState()

        # QoS: HiWonder 节点一般用 sensor_data 兼容，订阅端用同一个
        self.create_subscription(
            BatteryState, '/ros_robot_controller/battery',
            self._on_battery, qos_profile_system_default)
        self.create_subscription(
            Imu, '/imu', self._on_imu, qos_profile_system_default)
        self.create_subscription(
            Odometry, '/odom', self._on_odom, qos_profile_system_default)
        self.create_subscription(
            Twist, cmd_topic, self._on_cmd, qos_profile_system_default)

        self._print_timer = self.create_timer(1.0 / max(print_hz, 0.1), self._print_status)

        self.get_logger().info(
            f'monitor_node up — print_hz={print_hz} cmd_topic={cmd_topic}')

    # ---- callbacks ----

    def _stamp(self, name: str) -> None:
        self.state.last_topic_seen[name] = time.time()

    def _on_battery(self, msg: BatteryState) -> None:
        if msg.voltage is not None and msg.voltage > 0:
            self.state.battery_voltage = float(msg.voltage)
        if msg.percentage is not None and msg.percentage >= 0:
            self.state.battery_percentage = float(msg.percentage) * 100.0
        self.state.battery_stamp = msg.header.stamp.sec + msg.header.stamp.nanosec * 1e-9
        self._stamp('/battery')

    def _on_imu(self, msg: Imu) -> None:
        r, p, y = quat_to_rpy(
            msg.orientation.x, msg.orientation.y, msg.orientation.z, msg.orientation.w)
        self.state.imu_roll = math.degrees(r)
        self.state.imu_pitch = math.degrees(p)
        self.state.imu_yaw = math.degrees(y)
        self.state.imu_stamp = msg.header.stamp.sec + msg.header.stamp.nanosec * 1e-9
        self._stamp('/imu')

    def _on_odom(self, msg: Odometry) -> None:
        self.state.odom_x = msg.pose.pose.position.x
        self.state.odom_y = msg.pose.pose.position.y
        q = msg.pose.pose.orientation
        _, _, yaw = quat_to_rpy(q.x, q.y, q.z, q.w)
        self.state.odom_yaw = math.degrees(yaw)
        self.state.odom_v = float(msg.twist.twist.linear.x)
        self.state.odom_w = float(msg.twist.twist.angular.z)
        self.state.odom_stamp = msg.header.stamp.sec + msg.header.stamp.nanosec * 1e-9
        self._stamp('/odom')

    def _on_cmd(self, msg: Twist) -> None:
        self.state.cmd_v = float(msg.linear.x)
        self.state.cmd_w = float(msg.angular.z)
        self.state.cmd_stamp = time.time()
        self._stamp('cmd_vel')

    # ---- print ----

    def _format_status(self) -> str:
        s = self.state
        now = time.time()
        stale = lambda t: 'STALE' if (t and now - t > self._stale_sec) else 'fresh'

        def fmt(v, unit='', fmt='.2f'):
            return f'{v:{fmt}}{unit}' if v is not None else '   --  '

        line1 = (
            f'bat={fmt(s.battery_voltage, "V")} '
            f'{fmt(s.battery_percentage, "%")} '
            f'pose=({fmt(s.odom_x)}, {fmt(s.odom_y)}, yaw={fmt(s.odom_yaw, "deg", ".0f")})'
        )
        line2 = (
            f'imu  R/P/Y={fmt(s.imu_roll, "deg", ".0f")}/{fmt(s.imu_pitch, "deg", ".0f")}/{fmt(s.imu_yaw, "deg", ".0f")} '
            f'vel=({fmt(s.odom_v, "m/s")}, {fmt(s.odom_w, "rad/s")})'
        )
        line3 = (
            f'cmd  v={fmt(s.cmd_v, "m/s")} w={fmt(s.cmd_w, "rad/s")}'
        )
        # 哪些话题收到了
        topics = []
        for t, ts in s.last_topic_seen.items():
            topics.append(f'{t}={stale(ts)}')
        line4 = 'topics: ' + ', '.join(topics) if topics else 'topics: --'

        return '\n'.join([line1, line2, line3, line4])

    def _print_status(self) -> None:
        self.get_logger().info('\n' + self._format_status())


def main(args=None) -> None:
    rclpy.init(args=args)
    node = MonitorNode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()