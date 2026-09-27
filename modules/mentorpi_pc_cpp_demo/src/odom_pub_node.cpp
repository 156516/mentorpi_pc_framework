// 演示：订阅 /odom，周期性发布一个测试用 /cmd_vel（默认 v=0.1, w=0）。
// 真实用途：手动接管时方便观察 cmd_vel 通路。
// 注意：这是演示代码，别在车没看管的时候跑——可能让车移动。

#include <chrono>
#include <memory>

#include "rclcpp/rclcpp.hpp"
#include "geometry_msgs/msg/twist.hpp"
#include "nav_msgs/msg/odometry.hpp"

using namespace std::chrono_literals;

class OdomPubNode : public rclcpp::Node {
public:
    OdomPubNode()
        : Node("odom_pub_node")
    {
        this->declare_parameter<std::string>("cmd_topic", "/app/cmd_vel");
        this->declare_parameter<double>("linear_x", 0.0);
        this->declare_parameter<double>("angular_z", 0.0);
        this->declare_parameter<double>("publish_hz", 10.0);

        const auto cmd_topic = this->get_parameter("cmd_topic").as_string();
        const double publish_hz = this->get_parameter("publish_hz").as_double();

        cmd_pub_ = this->create_publisher<geometry_msgs::msg::Twist>(
            cmd_topic, 10);

        // 收到 odom 才发——证明通路 OK
        odom_sub_ = this->create_subscription<nav_msgs::msg::Odometry>(
            "/odom", rclcpp::SystemDefaultsQoS(),
            [this](const nav_msgs::msg::Odometry::SharedPtr) {
                geometry_msgs::msg::Twist t;
                t.linear.x  = this->get_parameter("linear_x").as_double();
                t.angular.z = this->get_parameter("angular_z").as_double();
                cmd_pub_->publish(t);
            });

        // 默认 zero rate 模式（不发布任何速度）
        if (publish_hz > 0.0) {
            const auto period = std::chrono::duration<double>(1.0 / publish_hz);
            timer_ = this->create_wall_timer(
                std::chrono::duration_cast<std::chrono::nanoseconds>(period),
                [this]() {
                    geometry_msgs::msg::Twist t;
                    t.linear.x  = this->get_parameter("linear_x").as_double();
                    t.angular.z = this->get_parameter("angular_z").as_double();
                    cmd_pub_->publish(t);
                });
        }

        RCLCPP_WARN(this->get_logger(),
            "odom_pub_node up — cmd_topic=%s linear_x=%.2f angular_z=%.2f (默认 0 = 不动)",
            cmd_topic.c_str(),
            this->get_parameter("linear_x").as_double(),
            this->get_parameter("angular_z").as_double());
    }

private:
    rclcpp::Publisher<geometry_msgs::msg::Twist>::SharedPtr cmd_pub_;
    rclcpp::Subscription<nav_msgs::msg::Odometry>::SharedPtr odom_sub_;
    rclcpp::TimerBase::SharedPtr timer_;
};

int main(int argc, char * argv[])
{
    rclcpp::init(argc, argv);
    rclcpp::spin(std::make_shared<OdomPubNode>());
    rclcpp::shutdown();
    return 0;
}