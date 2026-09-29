// 主窗口 ViewModel
// 订阅 ROS 话题，更新 UI

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
    [ObservableProperty] private WriteableBitmap? _mapImage;
    [ObservableProperty] private string _mapInfo = "等待 /map...";

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

        // /map 来自 PC 端 slam_toolbox（见 modules/mentorpi_pc_slam_toolbox）
        // slam_toolbox 默认 qos 是 reliable，跟其他 HiWonder 节点一致
        await _ros.SubscribeAsync<OccupancyGrid>(
            "/map", "nav_msgs/msg/OccupancyGrid", msg =>
        {
            // 跨线程更新 UI 属性，slam_toolbox publisher 跟 GUI 不在同一线程
            Dispatcher.UIThread.Post(() => RenderMap(msg));
        });
    }

    private void RenderMap(OccupancyGrid grid)
    {
        var info = grid.Info;
        int w = info.Width;
        int h = info.Height;
        if (w <= 0 || h <= 0 || grid.Data.Length != w * h)
        {
            MapInfo = $"地图异常: {w}x{h}, data={grid.Data.Length}";
            return;
        }

        // 缓存复用：尺寸未变就刷新现有 bitmap，避免每帧 GC 压力
        var bmp = MapImage;
        if (bmp is null || bmp.PixelSize.Width != w || bmp.PixelSize.Height != h)
        {
            bmp = new WriteableBitmap(
                new Avalonia.PixelSize(w, h),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Unpremul);
            MapImage = bmp;
        }

        // 像素映射：
        //   -1（未知）→ 0xFF808080 中灰
        //    0（空闲）→ 0xFFFFFFFF 白
        //   100（占据）→ 0xFF000000 黑
        //   其他      → 按比例（中间值）
        using (var fb = bmp.Lock())
        {
            var pixels = new int[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                int v = grid.Data[i];
                int argb;
                if (v < 0)        argb = unchecked((int)0xFF808080); // 未知
                else if (v == 0)  argb = unchecked((int)0xFFFFFFFF); // 空闲
                else if (v >= 100) argb = unchecked((int)0xFF000000); // 占据
                else              argb = unchecked((int)0xFF000000 | ((255 - v * 255 / 100) * 0x010101)); // 灰阶
                pixels[i] = argb;
            }
            Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
        }

        MapInfo = $"地图 {w}x{h} @ {info.Resolution:F3} m/px";
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