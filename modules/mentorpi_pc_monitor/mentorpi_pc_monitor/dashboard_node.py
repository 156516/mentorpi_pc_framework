"""
PyQt5 仪表板（实验性）。订阅同 monitor_node 的话题，
以 qtgraph 画实时曲线 / pyqt 信号槽刷电池条。

跑：
    ros2 run mentorpi_pc_monitor dashboard_node

依赖：PyQt5 + pyqtgraph（已在 .venv 里）
"""

from __future__ import annotations

import sys
from collections import deque
from typing import Optional

import rclpy
from rclpy.node import Node
from rclpy.qos import qos_profile_system_default
from sensor_msgs.msg import BatteryState, Imu
from nav_msgs.msg import Odometry
from geometry_msgs.msg import Twist

from PyQt5.QtCore import QTimer, Qt
from PyQt5.QtGui import QFont
from PyQt5.QtWidgets import (
    QApplication, QWidget, QVBoxLayout, QHBoxLayout,
    QLabel, QProgressBar, QFrame,
)

try:
    import pyqtgraph as pg
    HAS_PG = True
except ImportError:
    HAS_PG = False

from monitor_node import RobotState, quat_to_rpy
import math
import time


class RosSpinThread:
    """在主线程跑 rclpy.spin_once 的轻量包装——避免开线程同步问题。"""

    def __init__(self, node: Node, hz: int = 30):
        self.node = node
        self.period_ms = max(1, int(1000 / hz))
        self._timer: Optional[QTimer] = None

    def attach(self, qt_timer: QTimer) -> None:
        qt_timer.setInterval(self.period_ms)
        qt_timer.timeout.connect(self._tick)

    def _tick(self) -> None:
        rclpy.spin_once(self.node, timeout_sec=0.0)


class DashboardWindow(QWidget):
    def __init__(self, state: RobotState):
        super().__init__()
        self.state = state
        self.setWindowTitle('MentorPi PC Dashboard')
        self.resize(900, 600)

        # ---- 顶部状态卡片 ----
        top = QHBoxLayout()

        # 电池
        bat_box = QFrame()
        bat_box.setFrameShape(QFrame.StyledPanel)
        bat_v = QVBoxLayout(bat_box)
        bat_v.addWidget(QLabel('电池'))
        self.bat_v_label = QLabel('-- V')
        self.bat_v_label.setFont(QFont('Consolas', 18, QFont.Bold))
        bat_v.addWidget(self.bat_v_label)
        self.bat_bar = QProgressBar()
        self.bat_bar.setRange(0, 100)
        bat_v.addWidget(self.bat_bar)

        # 速度
        vel_box = QFrame()
        vel_box.setFrameShape(QFrame.StyledPanel)
        vel_v = QVBoxLayout(vel_box)
        vel_v.addWidget(QLabel('速度'))
        self.vel_label = QLabel('v=-- m/s\nw=-- rad/s')
        self.vel_label.setFont(QFont('Consolas', 14))
        vel_v.addWidget(self.vel_label)

        # IMU
        imu_box = QFrame()
        imu_box.setFrameShape(QFrame.StyledPanel)
        imu_v = QVBoxLayout(imu_box)
        imu_v.addWidget(QLabel('IMU 姿态'))
        self.imu_label = QLabel('R=--°\nP=--°\nY=--°')
        self.imu_label.setFont(QFont('Consolas', 14))
        imu_v.addWidget(self.imu_label)

        top.addWidget(bat_box, 1)
        top.addWidget(vel_box, 1)
        top.addWidget(imu_box, 1)

        # ---- 中间曲线 ----
        if HAS_PG:
            self.plot = pg.PlotWidget(title='cmd_vel & odom velocity (last 30s)')
            self.plot.showGrid(x=True, y=True)
            self.plot.setLabel('left', 'linear x', units='m/s')
            self.plot.addLegend()
            self.cmd_curve = self.plot.plot(pen='y', name='cmd_v')
            self.odom_curve = self.plot.plot(pen='c', name='odom_v')
            self.t0 = time.time()
            self.t_data = deque(maxlen=600)
            self.cmd_data = deque(maxlen=600)
            self.odom_data = deque(maxlen=600)
        else:
            self.plot = QLabel('pyqtgraph 没装 — 仅显示数字')

        # ---- 整体布局 ----
        layout = QVBoxLayout(self)
        layout.addLayout(top)
        layout.addWidget(self.plot, 1)
        self.setLayout(layout)

    def update_view(self) -> None:
        s = self.state
        if s.battery_voltage is not None:
            self.bat_v_label.setText(f'{s.battery_voltage:.2f} V')
        if s.battery_percentage is not None:
            self.bat_bar.setValue(int(s.battery_percentage))
        self.vel_label.setText(f'v={s.odom_v:+.2f} m/s\nw={s.odom_w:+.2f} rad/s')
        if s.imu_roll is not None:
            self.imu_label.setText(
                f'R={s.imu_roll:+.0f}°\nP={s.imu_pitch:+.0f}°\nY={s.imu_yaw:+.0f}°')

        if HAS_PG:
            now = time.time() - self.t0
            self.t_data.append(now)
            self.cmd_data.append(s.cmd_v)
            self.odom_data.append(s.odom_v)
            self.cmd_curve.setData(list(self.t_data), list(self.cmd_data))
            self.odom_curve.setData(list(self.t_data), list(self.odom_data))
            self.plot.setXRange(max(0, now - 30), now)


class DashboardNode(Node):
    """ROS2 节点 + Qt 主线程协同——rclpy spin_once 由 QTimer 驱动。"""

    def __init__(self) -> None:
        super().__init__('mentorpi_pc_dashboard')
        self.declare_parameter('cmd_topic', '/app/cmd_vel')
        cmd_topic = str(self.get_parameter('cmd_topic').value)

        self.state = RobotState()

        self.create_subscription(
            BatteryState, '/ros_robot_controller/battery',
            self._on_battery, qos_profile_system_default)
        self.create_subscription(
            Imu, '/imu', self._on_imu, qos_profile_system_default)
        self.create_subscription(
            Odometry, '/odom', self._on_odom, qos_profile_system_default)
        self.create_subscription(
            Twist, cmd_topic, self._on_cmd, qos_profile_system_default)

        self.app = QApplication.instance() or QApplication(sys.argv)
        self.window = DashboardWindow(self.state)

        self._spin = RosSpinThread(self)
        self._qt_timer = QTimer()
        self._spin.attach(self._qt_timer)

        self._qt_timer.start()
        self._qt_timer.timeout.connect(self.window.update_view)

        self.get_logger().info('dashboard_node up — opening Qt window')

    def _on_battery(self, msg):
        if msg.voltage and msg.voltage > 0:
            self.state.battery_voltage = float(msg.voltage)
        if msg.percentage is not None and msg.percentage >= 0:
            self.state.battery_percentage = float(msg.percentage) * 100.0

    def _on_imu(self, msg):
        r, p, y = quat_to_rpy(
            msg.orientation.x, msg.orientation.y, msg.orientation.z, msg.orientation.w)
        self.state.imu_roll, self.state.imu_pitch, self.state.imu_yaw = (
            math.degrees(r), math.degrees(p), math.degrees(y))

    def _on_odom(self, msg):
        self.state.odom_x = msg.pose.pose.position.x
        self.state.odom_y = msg.pose.pose.position.y
        self.state.odom_v = float(msg.twist.twist.linear.x)
        self.state.odom_w = float(msg.twist.twist.angular.z)

    def _on_cmd(self, msg):
        self.state.cmd_v = float(msg.linear.x)
        self.state.cmd_w = float(msg.angular.z)


def main(args=None) -> None:
    rclpy.init(args=args)
    node = DashboardNode()
    try:
        node.window.show()
        # 进入 Qt 事件循环（里面靠 QTimer 调用 spin_once）
        rc = node.app.exec_()
    except KeyboardInterrupt:
        rc = 0
    finally:
        node.destroy_node()
        rclpy.try_shutdown()
    sys.exit(rc)


if __name__ == '__main__':
    main()