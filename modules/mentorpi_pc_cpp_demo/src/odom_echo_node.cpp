// 演示：订阅 /odom，定期打印当前位姿。

#include <chrono>
#include <memory>
#include <string>

#include "rclcpp/rclcpp.hpp"
#include "nav_msgs/msg/odometry.hpp"
#include "mentorpi_pc_cpp_demo/odom_echo_lib.hpp"

using namespace std::chrono_literals;

class OdomEchoNode : public rclcpp::Node {
public:
    OdomEchoNode()
        : Node("odom_echo_node")
    {
        this->declare_parameter<std::string>("odom_topic", "/odom");
        this->declare_parameter<double>("print_hz", 1.0);

        const auto odom_topic = this->get_parameter("odom_topic").as_string();
        const double print_hz = this->get_parameter("print_hz").as_double();

        using std::placeholders::_1;
        sub_ = this->create_subscription<nav_msgs::msg::Odometry>(
            odom_topic, rclcpp::SystemDefaultsQoS(),
            std::bind(&OdomEchoNode::on_odom, this, _1));

        const auto period = std::chrono::duration<double>(1.0 / std::max(print_hz, 0.1));
        timer_ = this->create_wall_timer(
            std::chrono::duration_cast<std::chrono::nanoseconds>(period),
            std::bind(&OdomEchoNode::print_state, this));

        RCLCPP_INFO(this->get_logger(),
            "odom_echo_node up — topic=%s print_hz=%.1f",
            odom_topic.c_str(), print_hz);
    }

private:
    void on_odom(const nav_msgs::msg::Odometry::SharedPtr msg)
    {
        last_odom_ = *msg;
        have_odom_ = true;
        int a = 3;
    }

    void print_state()
    {
        if (!have_odom_) {
            RCLCPP_INFO_THROTTLE(this->get_logger(), *this->get_clock(), 2000,
                "waiting for odom...");
            return;
        }
        const auto & p = last_odom_.pose.pose.position;
        const double yaw = mentorpi_pc_cpp_demo::odom_yaw(last_odom_);
        RCLCPP_INFO(this->get_logger(),
            "x=%.3f y=%.3f yaw=%.1fdeg",
            p.x, p.y, yaw * 180.0 / M_PI);
    }

    rclcpp::Subscription<nav_msgs::msg::Odometry>::SharedPtr sub_;
    rclcpp::TimerBase::SharedPtr timer_;
    nav_msgs::msg::Odometry last_odom_;
    bool have_odom_ = false;
};

int main(int argc, char * argv[])
{
    rclcpp::init(argc, argv);
    rclcpp::spin(std::make_shared<OdomEchoNode>());
    rclcpp::shutdown();
    return 0;
}
