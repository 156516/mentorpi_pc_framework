FROM mentorpi_pc/ros2-base:latest

WORKDIR /workspace/src/mentorpi_description
COPY . /workspace/src/mentorpi_description/

RUN bash -c "source /opt/ros/humble/setup.bash && cd /workspace \
 && colcon build --symlink-install --packages-select mentorpi_description --cmake-args -DCMAKE_BUILD_TYPE=Release"

# description 包主要是 URDF + meshes + launch 模板，不一定需要常驻容器
# 用默认 bash，用户进来 source + ros2 launch
CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && bash"]