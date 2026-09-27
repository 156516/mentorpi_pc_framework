FROM mentorpi_pc/ros2-base:latest

WORKDIR /workspace/src/mentorpi_pc_monitor
COPY . /workspace/src/mentorpi_pc_monitor/

RUN bash -c "source /opt/ros/humble/setup.bash && cd /workspace \
 && colcon build --symlink-install --packages-select mentorpi_pc_monitor"

CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 launch mentorpi_pc_monitor monitor.launch.py"]