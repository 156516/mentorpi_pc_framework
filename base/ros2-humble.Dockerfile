# 公共 ROS2 Humble base 镜像
# 所有 ROS 功能包模块都基于这个
#
# build:
#   docker build -t mentorpi_pc/ros2-base:latest -f base/ros2-humble.Dockerfile .
#
# 一次性 build，所有 modules/ 共享（缓存复用）

FROM osrf/ros:humble-desktop

# 切阿里云源（避开校园网 DNS 劫持 archive.ubuntu.com）
RUN cp /etc/apt/sources.list /etc/apt/sources.list.bak.20260927 2>/dev/null || true \
 && echo "deb https://mirrors.aliyun.com/ubuntu/ jammy main restricted universe multiverse" > /etc/apt/sources.list \
 && echo "deb https://mirrors.aliyun.com/ubuntu/ jammy-updates main restricted universe multiverse" >> /etc/apt/sources.list \
 && echo "deb https://mirrors.aliyun.com/ubuntu/ jammy-security main restricted universe multiverse" >> /etc/apt/sources.list

RUN apt-get update \
 && apt-get install -y \
      python3-pip python3-venv \
      python3-colcon-common-extensions \
      python3-pyqt5 python3-pyqtgraph \
 && rm -rf /var/lib/apt/lists/*

# pip 用阿里云源
RUN pip config set global.index-url https://mirrors.aliyun.com/pypi/simple/

ENV ROS_DOMAIN_ID=0
ENV ROS_LOCALHOST_ONLY=0
ENV ROS_AUTOMATIC_DISCOVERY_RANGE=SUBNET

# ROS2 Humble 已经包含 rclpy / ament_python / geometry_msgs / sensor_msgs / nav_msgs
# build 命令：
#   source /opt/ros/humble/setup.bash && colcon build --symlink-install
WORKDIR /workspace