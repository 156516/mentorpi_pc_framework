# PC 端 slam_toolbox 模块
#
# 在树莓派不动的前提下，PC 端订阅 /scan_raw + /odom + /tf，做 2D 栅格建图，
# 发布 /map + map→odom TF。launch 里把 /scan_raw remap 成 /scan，喂 slam_toolbox。
#
# 用法:
#   bash mentorpi.sh slam                       # 起容器
#   bash mentorpi.sh save-map room1             # 把地图存到 ~/mentorpi_pc_framework/maps/room1.{pgm,yaml}

FROM mentorpi_pc/ros2-base:latest

# 装 slam_toolbox（apt 包，不是源码；HiWonder 树莓派端的 slam 包只是 launch + yaml 封装）
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ros-humble-slam-toolbox \
 && rm -rf /var/lib/apt/lists/*

# 拷源代码
WORKDIR /workspace/src/mentorpi_pc_slam_toolbox
COPY . /workspace/src/mentorpi_pc_slam_toolbox/

# colcon build（只 build 自己）
# 注意：COPY . 把仓库根整拷到 src/mentorpi_pc_slam_toolbox/，所以包实际在
# /workspace/src/mentorpi_pc_slam_toolbox/modules/mentorpi_pc_slam_toolbox/（嵌套）
RUN bash -c "source /opt/ros/humble/setup.bash && cd /workspace \
 && colcon build --symlink-install --packages-select mentorpi_pc_slam_toolbox \
 && cp -r /workspace/src/mentorpi_pc_slam_toolbox/modules/mentorpi_pc_slam_toolbox/config \
         /workspace/install/mentorpi_pc_slam_toolbox/share/mentorpi_pc_slam_toolbox/ \
 && cp -r /workspace/src/mentorpi_pc_slam_toolbox/modules/mentorpi_pc_slam_toolbox/launch \
         /workspace/install/mentorpi_pc_slam_toolbox/share/mentorpi_pc_slam_toolbox/"

# 地图保存目录（宿主的 ./maps 挂到这里）
RUN mkdir -p /workspace/maps

# 默认启动：source + 起 async mapper launch
CMD ["bash", "-lc", "source /opt/ros/humble/setup.bash \
 && source /workspace/install/setup.bash \
 && ros2 launch mentorpi_pc_slam_toolbox online_async.launch.py"]
