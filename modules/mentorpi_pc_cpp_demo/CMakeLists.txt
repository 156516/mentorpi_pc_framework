cmake_minimum_required(VERSION 3.16)
project(mentorpi_pc_cpp_demo LANGUAGES CXX)

# C++ 标准：ROS2 Humble 推荐 C++17
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ament_cmake 标配
if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release)
endif()

find_package(ament_cmake REQUIRED)
find_package(rclcpp REQUIRED)
find_package(geometry_msgs REQUIRED)
find_package(nav_msgs REQUIRED)

# 公共头（演示 include/ 目录用法）
add_library(odom_echo_lib SHARED
  src/odom_echo_lib.cpp
)
target_include_directories(odom_echo_lib PUBLIC
  $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
)
ament_target_dependencies(odom_echo_lib PUBLIC
  "rclcpp"
  "geometry_msgs"
  "nav_msgs"
)

# 节点 1：订阅 /odom 把当前位姿打到日志
add_executable(odom_echo_node src/odom_echo_node.cpp)
target_link_libraries(odom_echo_node odom_echo_lib)
ament_target_dependencies(odom_echo_node "rclcpp" "geometry_msgs" "nav_msgs")

# 节点 2：订阅 /odom 发布反向 /cmd_vel（演示 publish loop）
add_executable(odom_pub_node src/odom_pub_node.cpp)
target_link_libraries(odom_pub_node odom_echo_lib)
ament_target_dependencies(odom_pub_node "rclcpp" "geometry_msgs" "nav_msgs")

# 安装：可执行文件 + 库 + launch
install(TARGETS
  odom_echo_node
  odom_pub_node
  odom_echo_lib
  RUNTIME DESTINATION lib/${PROJECT_NAME}
  ARCHIVE DESTINATION lib/${PROJECT_NAME}
  LIBRARY DESTINATION lib/${PROJECT_NAME}
)
install(DIRECTORY launch/
  DESTINATION share/${PROJECT_NAME}/launch
  OPTIONAL
)

ament_package()