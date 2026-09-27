# 新算法模块的 Dockerfile 模板
# 用法：
#   cp -r modules/_template modules/my_cool_algo
#   mv modules/my_cool_algo/resource/_template modules/my_cool_algo/resource/my_cool_algo
#   改 package.xml / setup.py / 节点代码
#   docker-compose.yml 加一段：
#     my_cool_algo:
#       build: ./modules/my_cool_algo
#       network_mode: host
#       environment: [ROS_DOMAIN_ID=0]
#   docker compose up my_cool_algo

FROM mentorpi_pc/ros2-base:latest

# 拷源代码
WORKDIR /workspace/src/my_cool_algo
COPY . /workspace/src/my_cool_algo/

# 装这个包特有的 pip 依赖（如果有）
# RUN pip install requests numpy

# colcon build（只 build 自己）
RUN bash -c "source /opt/ros/humble/setup.bash && cd /workspace \
 && colcon build --symlink-install --packages-select my_cool_algo"

# 默认启动命令（容器跑起来时执行）
CMD ["bash", "-c", "source /opt/ros/humble/setup.bash && source /workspace/install/setup.bash && ros2 run my_cool_algo my_node"]