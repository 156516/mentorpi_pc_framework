FROM mentorpi_pc/ros2-base:latest

WORKDIR /workspace/src/obstacle_distance
COPY . /workspace/src/obstacle_distance/

RUN bash -c "source /opt/ros/humble/setup.bash && cd /workspace \
 && colcon build --symlink-install --packages-select obstacle_distance"

CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 launch obstacle_distance obstacle.launch.py"]