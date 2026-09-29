// ROS 消息 DTO —— 对应 rosbridge 下发的 JSON 字段（字段名与 ROS 消息定义一致）
// 只声明 GUI 用到的字段，其余字段反序列化时自动忽略。

namespace MentorpiPc.Gui.Messages;

// sensor_msgs/msg/BatteryState（部分字段）
public sealed class BatteryState
{
    public double Voltage { get; set; }
    public double Percentage { get; set; }   // 0.0 ~ 1.0
}

// std_msgs/msg/UInt16 — HiWonder /ros_robot_controller/battery 实际发的类型
public sealed class BatteryRaw
{
    public int Data { get; set; }
}

// nav_msgs/msg/Odometry（部分字段）
public sealed class Odometry
{
    public PoseWithCovariance Pose { get; set; } = new();
    public TwistWithCovariance Twist { get; set; } = new();
}

public sealed class PoseWithCovariance
{
    public Pose Pose { get; set; } = new();
}

public sealed class Pose
{
    public Point Position { get; set; } = new();
    public Quaternion Orientation { get; set; } = new();
}

public sealed class Point
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class TwistWithCovariance
{
    public Twist Twist { get; set; } = new();
}

public sealed class Twist
{
    public Vector3 Linear { get; set; } = new();
    public Vector3 Angular { get; set; } = new();
}

public sealed class Vector3
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

// sensor_msgs/msg/Imu（部分字段）
public sealed class Imu
{
    public Quaternion Orientation { get; set; } = new();
    public Vector3 AngularVelocity { get; set; } = new();
    public Vector3 LinearAcceleration { get; set; } = new();
}

// geometry_msgs/msg/Quaternion
public sealed class Quaternion
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double W { get; set; }
}

// nav_msgs/msg/OccupancyGrid（slam_toolbox /map）
// data: int8[] —— System.Text.Json byte[] 有特殊处理（base64），所以用 sbyte[]
//   -1 = 未知（渲染为中灰）/ 0 = 空闲（白）/ 100 = 占据（黑）
public sealed class OccupancyGrid
{
    public MapMetaData Info { get; set; } = new();
    public sbyte[] Data { get; set; } = System.Array.Empty<sbyte>();
}

public sealed class MapMetaData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double Resolution { get; set; }
    public Pose Origin { get; set; } = new();
}

// std_msgs/msg/String — slam_toolbox/srv/SaveMap request 的 name 字段就是它
public sealed class StdString
{
    public string Data { get; set; } = "";
}

// slam_toolbox/srv/SaveMap（部分字段）
// req.name 是不含扩展名的完整路径（如 /workspace/maps/room1）
// resp.result: 0=SUCCESS, 1=NO_MAP_RECEIVED, 255=UNDEFINED_FAILURE
public sealed class SaveMapRequest
{
    public StdString Name { get; set; } = new();
}

public sealed class SaveMapResponse
{
    public int Result { get; set; }
}
