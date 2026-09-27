// 主窗口 ViewModel
// 订阅 ROS 话题，更新 UI

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MentorpiPc.Gui.Messages;
using MentorpiPc.Gui.Services;

namespace MentorpiPc.Gui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly RosService _ros;

    [ObservableProperty] private string _connectionStatus = "未连接";
    [ObservableProperty] private double _batteryVoltage;
    [ObservableProperty] private double _batteryPercentage;
    [ObservableProperty] private double _linearVel;
    [ObservableProperty] private double _angularVel;
    [ObservableProperty] private double _imuRoll;
    [ObservableProperty] private double _imuPitch;
    [ObservableProperty] private double _imuYaw;

    public MainViewModel(RosService ros)
    {
        _ros = ros;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            ConnectionStatus = "连接中...";
            await _ros.ConnectAsync();
            ConnectionStatus = "已连接";
            await SubscribeTopicsAsync();
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"连接失败: {ex.Message}";
        }
    }

    private async Task SubscribeTopicsAsync()
    {
        // /ros_robot_controller/battery 实际发 std_msgs/UInt16（百分比 × 100，如 7948 = 79.48%）
        // HiWonder 12V 铅酸经验估算：10.5V 截止、13.5V 满电
        await _ros.SubscribeAsync<BatteryRaw>(
            "/ros_robot_controller/battery", "std_msgs/msg/UInt16", msg =>
        {
            double raw = msg.Data;
            double pct;
            if (raw <= 100.0) pct = raw;
            else if (raw <= 10000.0) pct = raw / 100.0;
            else pct = Math.Min(100.0, raw / 1000.0);
            BatteryVoltage = 10.5 + (pct / 100.0) * (13.5 - 10.5);
            BatteryPercentage = pct;
        });

        await _ros.SubscribeAsync<Odometry>(
            "/odom", "nav_msgs/msg/Odometry", msg =>
        {
            LinearVel = msg.Twist.Twist.Linear.X;
            AngularVel = msg.Twist.Twist.Angular.Z;
        });

        await _ros.SubscribeAsync<Imu>(
            "/imu", "sensor_msgs/msg/Imu", msg =>
        {
            var (r, p, y) = QuatToRpy(msg.Orientation);
            ImuRoll = r;
            ImuPitch = p;
            ImuYaw = y;
        });
    }

    private static (double, double, double) QuatToRpy(Quaternion q)
    {
        var x = q.X;
        var y = q.Y;
        var z = q.Z;
        var w = q.W;
        var roll = Math.Atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y));
        var sinp = 2 * (w * y - z * x);
        var pitch = Math.Abs(sinp) >= 1
            ? Math.CopySign(Math.PI / 2, sinp)
            : Math.Asin(sinp);
        var yaw = Math.Atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z));
        return (roll * 180 / Math.PI, pitch * 180 / Math.PI, yaw * 180 / Math.PI);
    }
}
