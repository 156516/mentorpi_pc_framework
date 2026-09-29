# rosbridge_server: 把 ROS2 话题转成 WebSocket
# 后续 Windows GUI 也能用 ws:// 连进来（不依赖 .NET on Linux）

FROM osrf/ros:humble-desktop

# 装 rosbridge_server（slam_toolbox apt 包只含 C++ lib/header，没 Python 模块——见下）
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ros-humble-rosbridge-server \
 && rm -rf /var/lib/apt/lists/*

# 拷 slam_toolbox Python 包 —— rosbridge 需要它才能解析
# /slam_toolbox/* service 的 response 类型（dynamic_map / save_map 等）。
# 直接 apt 装 ros-humble-slam-toolbox 不行——它只装 C++ 部分，没有 Python wheels。
# 来源：从 slam_toolbox 镜像里 docker cp 出来的 slam_toolbox_pkg/ 目录，路径：
#   /opt/ros/humble/local/lib/python3.10/dist-packages/slam_toolbox/
# 把它直接展开到镜像的 dist-packages/ 下（保留所有 .so / srv 子目录等）。
COPY services/rosbridge/slam_toolbox_pkg/ /opt/ros/humble/local/lib/python3.10/dist-packages/

# 自定义 launch（覆盖 apt 装的 rosbridge_server 默认 launch，启用多线程 + service_timeout）
COPY services/rosbridge/rosbridge_websocket_launch.xml /opt/ros/humble/share/rosbridge_server/launch/rosbridge_websocket_launch.xml

# 让 Python 能 import slam_toolbox（setup.bash 不加 local/dist-packages）
ENV PYTHONPATH=/opt/ros/humble/local/lib/python3.10/dist-packages:${PYTHONPATH}

ENV ROS_DOMAIN_ID=0
ENV ROS_LOCALHOST_ONLY=0

CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && ros2 launch rosbridge_server rosbridge_websocket_launch.xml"]