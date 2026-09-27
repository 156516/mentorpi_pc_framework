FROM mentorpi_pc/ros2-base:latest

WORKDIR /workspace/src/mentorpi_pc_cpp_demo
COPY . /workspace/src/mentorpi_pc_cpp_demo/

RUN bash -c "source /opt/ros/humble/setup.bash && cd /workspace \
 && colcon build --symlink-install --packages-select mentorpi_pc_cpp_demo --cmake-args -DCMAKE_BUILD_TYPE=Release"

CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 launch mentorpi_pc_cpp_demo cpp_demo.launch.py"]