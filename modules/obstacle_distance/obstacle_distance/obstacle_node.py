"""
obstacle_distance 算法节点

订阅 /scan（激光）和 /odom（里程计），
按车体坐标系把激光点云分到 3 个 90° 扇形（前/左/右），
取每个扇形内的最小距离，每秒打印一次。

这是一个**完整算法模块示例**：
- 订阅多话题
- 处理数据（核心算法）
- 周期性输出
- 打印结果（也可用 topic publish）

参数：
- scan_topic  (default /scan)
- odom_topic  (default /odom)
- print_hz    (default 1.0)
- safe_distance (default 0.5)  # 距障碍物 < 此值报警
"""
from __future__ import annotations

import math
from dataclasses import dataclass

import rclpy
from rclpy.node import Node
from rclpy.qos import qos_profile_system_default
from sensor_msgs.msg import LaserScan
from nav_msgs.msg import Odometry


@dataclass
class SectorMin:
    """一个扇形内的最小距离。"""
    name: str
    min_distance: float = float('inf')
    angle_at_min: float = 0.0
    point_count: int = 0

    def to_str(self) -> str:
        d = f'{self.min_distance:.2f}m' if math.isfinite(self.min_distance) else '  --  '
        return f'{self.name}={d}'


class ObstacleNode(Node):
    """算法节点：算车体前/左/右最近障碍物。"""

    # 车体坐标系下三个扇形中心（弧度）
    # ROS REP-103: x 前 / y 左
    SECTOR_FRONT_CENTER = 0.0       # 前方
    SECTOR_LEFT_CENTER = math.pi / 2  # 左方
    SECTOR_RIGHT_CENTER = -math.pi / 2  # 右方
    SECTOR_HALF_WIDTH = math.pi / 4   # ±45°（每个扇形 90°）

    def __init__(self) -> None:
        super().__init__('obstacle_distance')

        self.declare_parameter('scan_topic', '/scan')
        self.declare_parameter('odom_topic', '/odom')
        self.declare_parameter('print_hz', 1.0)
        self.declare_parameter('safe_distance', 0.5)

        scan_topic = str(self.get_parameter('scan_topic').value)
        odom_topic = str(self.get_parameter('odom_topic').value)
        print_hz = float(self.get_parameter('print_hz').value)
        self._safe_distance = float(self.get_parameter('safe_distance').value)

        # 用 QoS sensor_data（树莓派 /scan 一般用这个）
        self._scan_sub = self.create_subscription(
            LaserScan, scan_topic, self._on_scan, qos_profile_system_default)

        # /odom 不订阅也行——只用 /scan 就够
        # 这里订阅是为了示例多话题
        self._odom_sub = self.create_subscription(
            Odometry, odom_topic, self._on_odom, qos_profile_system_default)

        self._sectors = {
            'front': SectorMin(name='front'),
            'left':  SectorMin(name='left'),
            'right': SectorMin(name='right'),
        }

        period = 1.0 / max(print_hz, 0.1)
        self._timer = self.create_timer(period, self._print_status)

        self.get_logger().info(
            f'obstacle_distance up — scan={scan_topic} odom={odom_topic} '
            f'print_hz={print_hz} safe={self._safe_distance}m')

    # ---- 回调 ----

    def _on_scan(self, msg: LaserScan) -> None:
        """处理一帧激光，分到 3 个扇形。"""
        # 重置
        for s in self._sectors.values():
            s.min_distance = float('inf')
            s.angle_at_min = 0.0
            s.point_count = 0

        angle_min = msg.angle_min
        angle_inc = msg.angle_increment
        range_min = msg.range_min
        range_max = msg.range_max

        for i, r in enumerate(msg.ranges):
            # 跳过无效读数（inf / nan / 超量程）
            if math.isinf(r) or math.isnan(r):
                continue
            if r < range_min or r > range_max:
                continue

            angle = angle_min + i * angle_inc  # 激光坐标系下的角度
            sector = self._classify(angle)
            if sector is None:
                continue
            sector.point_count += 1
            if r < sector.min_distance:
                sector.min_distance = r
                sector.angle_at_min = angle

    def _on_odom(self, msg: Odometry) -> None:
        """订阅 /odom 用于演示多话题——这里只是记录一下最新里程计。"""
        # 实际算法只用 /scan 就够；
        # /odom 可以用来做"车在动的时候不算障碍物"等扩展。
        self._latest_odom = msg

    # ---- 分类 ----

    def _classify(self, angle: float) -> SectorMin | None:
        """判断激光点在哪个扇形。"""
        for sector in self._sectors.values():
            center = {
                'front': self.SECTOR_FRONT_CENTER,
                'left':  self.SECTOR_LEFT_CENTER,
                'right': self.SECTOR_RIGHT_CENTER,
            }[sector.name]
            diff = self._wrap_pi(angle - center)
            if abs(diff) <= self.SECTOR_HALF_WIDTH:
                return sector
        return None

    @staticmethod
    def _wrap_pi(a: float) -> float:
        """把角度 wrap 到 (-π, π]。"""
        while a > math.pi:
            a -= 2 * math.pi
        while a <= -math.pi:
            a += 2 * math.pi
        return a

    # ---- 打印 ----

    def _print_status(self) -> None:
        f = self._sectors['front']
        l = self._sectors['left']
        r = self._sectors['right']

        # 报警：任一扇形内距离 < safe
        alarms = []
        for s in (f, l, r):
            if math.isfinite(s.min_distance) and s.min_distance < self._safe_distance:
                alarms.append(s.name)

        alarm_str = ' ⚠️ ALARM:' + ','.join(alarms) if alarms else ''

        self.get_logger().info(
            f'{f.to_str()}  {l.to_str()}  {r.to_str()}'
            f'   pts(f/l/r)={f.point_count}/{l.point_count}/{r.point_count}'
            f'{alarm_str}')


def main(args=None) -> None:
    rclpy.init(args=args)
    node = ObstacleNode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()