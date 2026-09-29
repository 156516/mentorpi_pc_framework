// 主窗口 ViewModel
// 订阅 ROS 话题，更新 UI；调用 ROS service 控制 slam_toolbox

using System;
using System.Collections.Generic;
using System.IO;
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

    // 地图控制按钮
    [ObservableProperty] private bool _mappingActive = true;
    [ObservableProperty] private string _mapControlStatus = "等待操作";
    [ObservableProperty] private string _mappingButtonText = "暂停建图";
    [ObservableProperty] private string _importedMapName = "";
    [ObservableProperty] private WriteableBitmap? _importedMapImage;
    [ObservableProperty] private string _importedMapInfo = "";

    // 小车在导入地图上的实时位置（像素坐标系：左上原点）
    // - CarPixelX/Y：地图 Image 控件内的坐标
    // - CarYawDeg：箭头朝向（度，0=右，90=上，对应 ROS 坐标 Y 朝上）
    // - HasCarPosition：是否已拿到有效 TF
    [ObservableProperty] private double _carPixelX;
    [ObservableProperty] private double _carPixelY;
    [ObservableProperty] private double _carYawDeg;
    [ObservableProperty] private double _carDotPixelX;  // = CarPixelX - 4（椭圆中心对准点）
    [ObservableProperty] private double _carDotPixelY;  // = CarPixelY - 4
    [ObservableProperty] private bool _hasCarPosition;

    // 缓存最近一次的 TF（用于拼接 map → base_footprint）
    private Transform? _mapToOdom;          // slam_toolbox 发布
    private Transform? _odomToBaseFootprint;// ekf_filter_node 发布（接近单位变换）

    // 导入地图参数（像素坐标系换算用）
    private double _importedMapOriginX;
    private double _importedMapOriginY;
    private double _importedMapResolution;
    private int _importedMapWidth;
    private int _importedMapHeight;

    public MainViewModel(RosService ros)
    {
        _ros = ros;
    }

    partial void OnMappingActiveChanged(bool value)
    {
        MappingButtonText = value ? "暂停建图" : "继续建图";
        MapControlStatus = value ? "slam_toolbox 处理中" : "slam_toolbox 已暂停";
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

        // /tf 拿 map → odom（slam_toolbox 发）和 odom → base_footprint（ekf 发）
        // 用于在地图上显示小车位置
        await _ros.SubscribeAsync<TFMessage>(
            "/tf", "tf2_msgs/msg/TFMessage", msg =>
        {
            Dispatcher.UIThread.Post(() => OnTransform(msg));
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

    // === 地图控制按钮 ===

    [RelayCommand]
    private async Task ToggleMappingAsync()
    {
        // /slam_toolbox/pause_new_measurements 是 toggle：每调一次翻转状态。
        // 调前先把 UI 状态翻转（乐观更新），调失败时回滚。
        var prev = MappingActive;
        MappingActive = !prev;
        MapControlStatus = "调 pause service...";
        try
        {
            // req 为空（结构占位字段），rosbridge 接受 {}
            var (ok, _) = await _ros.CallServiceAsync<object>("/slam_toolbox/pause_new_measurements", new { });
            if (!ok)
            {
                MappingActive = prev;
                MapControlStatus = "[错误] pause service 失败";
            }
            // 成功时 OnMappingActiveChanged 已经更新了 MappingButtonText 和 MapControlStatus
        }
        catch (Exception ex)
        {
            MappingActive = prev;
            MapControlStatus = $"[错误] pause 超时: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveMapAsync()
    {
        var name = $"map_{DateTime.Now:yyyyMMdd_HHmmss}";
        MapControlStatus = $"保存到 {name}...";
        try
        {
            var args = new
            {
                name = new { data = $"/workspace/maps/{name}" },
            };
            var (ok, resp) = await _ros.CallServiceAsync<SaveMapResponse>(
                "/slam_toolbox/save_map", args);
            if (ok && resp is not null && resp.Result == 0)
            {
                MapControlStatus = $"已保存 ~/mentorpi_pc_framework/maps/{name}.{{pgm,yaml}}";
            }
            else
            {
                var r = resp?.Result ?? -1;
                MapControlStatus = $"[错误] 保存失败: result={r} (1=NO_MAP, 255=FAIL)";
            }
        }
        catch (Exception ex)
        {
            MapControlStatus = $"[错误] 保存超时: {ex.Message}";
        }
    }

    /// <summary>
    /// 从磁盘 .yaml + .pgm 加载导入图（public，由 axaml.cs 的 File picker handler 调用）。
    /// 失败时抛异常 / 设 ImportedMapImage=null + 错误状态。
    /// 成功后还会从 yaml 抽出 origin/resolution/w/h，给「地图上标小车位置」用。
    /// </summary>
    public async Task LoadMapFromFileAsync(string yamlPath, string pgmPath)
    {
        try
        {
            var info = await Task.Run(() => ParseAndRenderMap(yamlPath, pgmPath));
            ImportedMapImage = info.bmp;
            ImportedMapInfo = info.summary;
            ImportedMapName = Path.GetFileNameWithoutExtension(yamlPath);
            MapControlStatus = $"已导入 {ImportedMapName} {info.summary}";

            // 解析 yaml 拿 origin/resolution/尺寸（用于坐标换算）
            var meta = await Task.Run(() => ReadYamlMeta(yamlPath));
            _importedMapOriginX = meta.originX;
            _importedMapOriginY = meta.originY;
            _importedMapResolution = meta.resolution;
            _importedMapWidth = info.bmp.PixelSize.Width;
            _importedMapHeight = info.bmp.PixelSize.Height;

            // 已导入图，立刻试算一次小车位置
            UpdateCarPosition();
        }
        catch (Exception ex)
        {
            ImportedMapImage = null;
            ImportedMapName = "";
            ImportedMapInfo = "";
            _importedMapOriginX = 0;
            _importedMapOriginY = 0;
            _importedMapResolution = 0;
            HasCarPosition = false;
            MapControlStatus = $"[错误] 导入失败: {ex.Message}";
        }
    }

    private static (double originX, double originY, double resolution) ReadYamlMeta(string yamlPath)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(yamlPath))
        {
            var t = line.Trim();
            if (t.StartsWith("#") || string.IsNullOrEmpty(t)) continue;
            var idx = t.IndexOf(':');
            if (idx < 0) continue;
            fields[t.Substring(0, idx).Trim()] = t.Substring(idx + 1).Trim();
        }
        double res = fields.TryGetValue("resolution", out var rs)
            ? double.Parse(rs, System.Globalization.CultureInfo.InvariantCulture) : 0.05;
        double ox = 0, oy = 0;
        if (fields.TryGetValue("origin", out var orig))
        {
            var nums = orig.Trim('[', ']').Split(',');
            if (nums.Length >= 1) ox = double.Parse(nums[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            if (nums.Length >= 2) oy = double.Parse(nums[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }
        return (ox, oy, res);
    }

    private static (WriteableBitmap bmp, string summary) ParseAndRenderMap(string yamlPath, string pgmPath)
    {
        // 解析 .yaml（手写——只取我们用得上的 6 个字段）
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(yamlPath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#") || string.IsNullOrEmpty(trimmed)) continue;
            var idx = trimmed.IndexOf(':');
            if (idx < 0) continue;
            var key = trimmed.Substring(0, idx).Trim();
            var value = trimmed.Substring(idx + 1).Trim();
            fields[key] = value;
        }

        if (!fields.TryGetValue("resolution", out var resStr))
            throw new InvalidDataException("yaml 缺 resolution 字段");
        if (!fields.TryGetValue("image", out var imageName))
            throw new InvalidDataException("yaml 缺 image 字段");
        double resolution = double.Parse(resStr, System.Globalization.CultureInfo.InvariantCulture);
        double ox = 0, oy = 0, oyaw = 0;
        if (fields.TryGetValue("origin", out var originStr))
        {
            var nums = originStr.Trim('[', ']').Split(',');
            if (nums.Length >= 1) ox = double.Parse(nums[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            if (nums.Length >= 2) oy = double.Parse(nums[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            if (nums.Length >= 3) oyaw = double.Parse(nums[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }
        int negate = fields.TryGetValue("negate", out var negStr) ? int.Parse(negStr) : 0;
        double occThresh = fields.TryGetValue("occupied_thresh", out var otStr)
            ? double.Parse(otStr, System.Globalization.CultureInfo.InvariantCulture) : 0.65;
        double freeThresh = fields.TryGetValue("free_thresh", out var ftStr)
            ? double.Parse(ftStr, System.Globalization.CultureInfo.InvariantCulture) : 0.25;

        // 解析 .pgm（P5 二进制 + 拿 width/height）
        var (w, h, pixels) = ReadPgm(pgmPath);

        // 按 yaml 三态规则：像素 → occupancy → ARGB
        // ROS 标准（negate=0）：occ = (1 - pixel/255) * 100
        //   occ >= occThresh*100 → 100（占据）
        //   occ <= freeThresh*100 → 0（空闲）
        //   其他 → -1（未知）
        var bmp = new WriteableBitmap(
            new Avalonia.PixelSize(w, h),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Unpremul);
        using (var fb = bmp.Lock())
        {
            var argbs = new int[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                double occv = (negate == 0 ? (1.0 - pixels[i] / 255.0) : (pixels[i] / 255.0)) * 100.0;
                int argb;
                if (occv >= occThresh * 100.0)      argb = unchecked((int)0xFF000000); // 占据 → 黑
                else if (occv <= freeThresh * 100.0) argb = unchecked((int)0xFFFFFFFF); // 空闲 → 白
                else                                argb = unchecked((int)0xFF808080); // 未知 → 中灰
                argbs[i] = argb;
            }
            Marshal.Copy(argbs, 0, fb.Address, argbs.Length);
        }

        var summary = $"{w}x{h} @ {resolution:F3} m/px, origin=[{ox:F2}, {oy:F2}, {oyaw:F2}°]";
        return (bmp, summary);
    }

    private static (int width, int height, byte[] pixels) ReadPgm(string path)
    {
        using var fs = File.OpenRead(path);
        // 头部：P5\n
        var magic = new byte[2];
        if (fs.Read(magic, 0, 2) != 2 || magic[0] != (byte)'P' || magic[1] != (byte)'5')
            throw new InvalidDataException("不是 P5 PGM");
        // 跳过空白和注释，读 width/height/maxval
        int w = int.Parse(ReadToken(fs), System.Globalization.CultureInfo.InvariantCulture);
        int h = int.Parse(ReadToken(fs), System.Globalization.CultureInfo.InvariantCulture);
        int maxval = int.Parse(ReadToken(fs), System.Globalization.CultureInfo.InvariantCulture);
        if (maxval != 255)
            throw new InvalidDataException($"只支持 maxval=255，当前 {maxval}");
        // ReadToken 已经把 maxval 后的空白跳过了——fs.Position 已经在第一个像素位置。
        // 别再 fs.ReadByte()，那会多吃 1 字节（导致 "像素数据不足: N-1/N"）。
        int read = 0;
        var data = new byte[w * h];
        while (read < data.Length)
        {
            int n = fs.Read(data, read, data.Length - read);
            if (n == 0) break;
            read += n;
        }
        if (read != data.Length)
            throw new InvalidDataException($"像素数据不足: {read}/{data.Length}");
        return (w, h, data);
    }

    private static string ReadToken(Stream s)
    {
        // 跳过空白和以 # 开头的注释行
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            int b = s.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            char c = (char)b;
            if (c == '#')
            {
                // 跳过整行注释
                while (b != '\n' && b != '\r' && b >= 0) b = s.ReadByte();
                continue;
            }
            if (char.IsWhiteSpace(c)) continue;
            // 开始读 token
            while (b >= 0 && !char.IsWhiteSpace((char)b))
            {
                sb.Append((char)b);
                b = s.ReadByte();
            }
            return sb.ToString();
        }
    }

    [RelayCommand]
    private void RestartSlamWithMap()
    {
        // 本次简化：按钮先放上，真正的「重启 slam_toolbox + 加载导入图作为初始」
        // 需要改 mentorpi.sh 加 load-map 子命令、改 docker-compose 给 slam 容器加 MAP_FILE_NAME 环境变量。
        // 当前 GUI 容器只有 dotnet runtime 没 docker CLI，所以即便要重启也只能通过 marker 文件中转。
        MapControlStatus = string.IsNullOrEmpty(ImportedMapName)
            ? "[提示] 请先点「导入图片」选地图"
            : $"[提示] 重启加载功能待实现；请手动跑 bash mentorpi.sh load-map {ImportedMapName}";
    }

    // === /tf 缓存 + 小车位置计算 ===

    private void OnTransform(TFMessage msg)
    {
        if (msg.Transforms is null) return;
        bool changed = false;
        foreach (var ts in msg.Transforms)
        {
            // 只缓存 map → odom 和 odom → base_footprint
            if (ts.FrameId == "map" && ts.ChildFrameId == "odom")
            {
                _mapToOdom = ts.Transform;
                changed = true;
            }
            else if (ts.FrameId == "odom" && ts.ChildFrameId == "base_footprint")
            {
                _odomToBaseFootprint = ts.Transform;
                changed = true;
            }
        }
        if (changed) UpdateCarPosition();
    }

    /// <summary>
    /// 用 map→odom + odom→base_footprint 拼出 map→base_footprint，再换算到像素。
    /// 没导入图时静默返回；缓存没齐时也静默返回。
    /// </summary>
    private void UpdateCarPosition()
    {
        if (_mapToOdom is null || _odomToBaseFootprint is null) return;
        if (ImportedMapImage is null || _importedMapResolution <= 0) return;

        // 拼接 map → base_footprint：先旋转 odom→base_footprint（绕 map 原点），再平移 map→odom
        // q_total = q1 * q2（map→odom ⊗ odom→base_footprint）
        var q1 = _mapToOdom.Rotation;
        var q2 = _odomToBaseFootprint.Rotation;
        double qw = q1.W * q2.W - q1.X * q2.X - q1.Y * q2.Y - q1.Z * q2.Z;
        double qx = q1.W * q2.X + q1.X * q2.W + q1.Y * q2.Z - q1.Z * q2.Y;
        double qy = q1.W * q2.Y - q1.X * q2.Z + q1.Y * q2.W + q1.Z * q2.X;
        double qz = q1.W * q2.Z + q1.X * q2.Y - q1.Y * q2.X + q1.Z * q2.W;

        // 平移：t_total = t1 + R1 * t2（R1 由 q1 旋转 t2）
        // 旋转 t2 用 q1：(0, t2.x, t2.y, t2.z) ← q1 ⊗ (0, t2.x, t2.y, t2.z) ⊗ q1*
        var t2 = _odomToBaseFootprint.Translation;
        double tx = q1.W * t2.X + q1.Y * t2.Z - q1.Z * t2.Y;
        double ty = q1.W * t2.Y - q1.X * t2.Z + q1.Z * t2.X;
        double tz = q1.W * t2.Z + q1.X * t2.Y - q1.Y * t2.X;
        // q1* (共轭：x,y,z 取负) 乘回去得到 R1*t2 的最终值
        double rotX = qw * (-q1.X) + qx * q1.W + qy * (-q1.Z) - qz * (-q1.Y);
        double rotY = qw * (-q1.Y) - qx * (-q1.Z) + qy * q1.W + qz * (-q1.X);
        double rotZ = qw * (-q1.Z) + qx * (-q1.Y) - qy * (-q1.X) + qz * q1.W;

        double px = _mapToOdom.Translation.X + rotX;
        double py = _mapToOdom.Translation.Y + rotY;

        // 像素坐标：
        //   pgm_x = (pose_x - origin.x) / resolution
        //   pgm_y = height - (pose_y - origin.y) / resolution  ← Y 翻转
        double pgmX = (px - _importedMapOriginX) / _importedMapResolution;
        double pgmY = _importedMapHeight - (py - _importedMapOriginY) / _importedMapResolution;

        // Yaw 转角度，ROS yaw=0 → 三角箭头朝右（X+），ROS yaw=π/2 → 朝上（Y+）
        // 像素坐标 Y 朝下，所以翻转 +90°
        (_, _, double yawRad) = QuatToRpy(new Quaternion { X = qx, Y = qy, Z = qz, W = qw });
        double yawDeg = yawRad * 180.0 / Math.PI;
        // 把 ROS 朝向映射到屏幕 Canvas 的 (X 右+, Y 下+)：ROS yaw=0 箭头朝右，Canvas 旋转 0 = 朝右，
        // 但 ROS yaw 增加是逆时针，Canvas 旋转增加是顺时针 → 取负
        double canvasDeg = -yawDeg;

        CarPixelX = pgmX;
        CarPixelY = pgmY;
        CarDotPixelX = pgmX - 4;  // 椭圆是 8x8，中心对准点需要 Left = X - 4
        CarDotPixelY = pgmY - 4;
        CarYawDeg = canvasDeg;
        HasCarPosition = true;
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