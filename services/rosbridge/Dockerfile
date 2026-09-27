# rosbridge_server: 把 ROS2 话题转成 WebSocket
# 后续 Windows GUI 也能用 ws:// 连进来（不依赖 .NET on Linux）

FROM osrf/ros:humble-desktop

RUN apt-get update \
 && apt-get install -y ros-humble-rosbridge-server \
 && rm -rf /var/lib/apt/lists/*

ENV ROS_DOMAIN_ID=0
ENV ROS_LOCALHOST_ONLY=0

CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && ros2 launch rosbridge_server rosbridge_websocket_launch.xml"]