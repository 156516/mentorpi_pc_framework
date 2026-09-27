#pragma once

#include <cmath>
#include <geometry_msgs/msg/quaternion.hpp>
#include <nav_msgs/msg/odometry.hpp>

namespace mentorpi_pc_cpp_demo {

/// 四元数 → 欧拉角（Z-Y-X），返回 (roll, pitch, yaw)，单位弧度。
inline std::tuple<double, double, double> quat_to_rpy(
    double x, double y, double z, double w)
{
    // roll (x)
    const double sinr_cosp = 2.0 * (w * x + y * z);
    const double cosr_cosp = 1.0 - 2.0 * (x * x + y * y);
    const double roll = std::atan2(sinr_cosp, cosr_cosp);

    // pitch (y)
    const double sinp = 2.0 * (w * y - z * x);
    const double pitch = (std::abs(sinp) >= 1.0)
        ? std::copysign(M_PI / 2.0, sinp)
        : std::asin(sinp);

    // yaw (z)
    const double siny_cosp = 2.0 * (w * z + x * y);
    const double cosy_cosp = 1.0 - 2.0 * (y * y + z * z);
    const double yaw = std::atan2(siny_cosp, cosy_cosp);

    return {roll, pitch, yaw};
}

/// 从 Odometry 取 yaw（弧度）。
inline double odom_yaw(const nav_msgs::msg::Odometry & odom)
{
    const auto & q = odom.pose.pose.orientation;
    auto [r, p, y] = quat_to_rpy(q.x, q.y, q.z, q.w);
    return y;
}

}  // namespace mentorpi_pc_cpp_demo