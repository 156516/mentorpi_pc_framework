// ROS2 客户端服务
// 用 rosbridge (WebSocket) 跨平台，直接实现 rosbridge JSON 协议（不依赖原生 rcl）

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using Websocket.Client;

namespace MentorpiPc.Gui.Services;

/// <summary>
/// ROS2 客户端封装。
/// 通过 rosbridge WebSocket 跟 ROS2 通讯，PC / Windows / Mac 都能用。
/// </summary>
public sealed class RosService : IAsyncDisposable
{
    private readonly string _bridgeUrl;
    private WebsocketClient? _client;
    private readonly Dictionary<string, List<Action<JsonElement>>> _handlers = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public bool IsConnected => _client?.IsRunning ?? false;

    public RosService(string bridgeUrl = "ws://localhost:9090")
    {
        _bridgeUrl = bridgeUrl;
    }

    public async Task ConnectAsync()
    {
        _client = new WebsocketClient(new Uri(_bridgeUrl))
        {
            ReconnectTimeout = null,
            ErrorReconnectTimeout = null,
        };
        _client.MessageReceived.Subscribe(OnMessageReceived);
        await _client.Start();
        if (!_client.IsRunning)
            throw new InvalidOperationException("无法连接 rosbridge: " + _bridgeUrl);
    }

    /// <summary>
    /// 订阅话题。收到消息后反序列化成 T 并回调 handler。
    /// type 为 ROS2 消息类型（如 "sensor_msgs/msg/BatteryState"）。
    /// </summary>
    public async Task SubscribeAsync<T>(string topic, string type, Action<T> handler)
        where T : new()
    {
        AddHandler(topic, e => handler(Deserialize<T>(e)));
        // qos.reliability=reliable：树莓派 HiWonder 节点用 RELIABLE 发布，
        // rosbridge 默认 best_effort 会收不到，必须显式指定 reliable。
        await SendAsync(new
        {
            op = "subscribe",
            topic,
            type,
            qos = new { reliability = "reliable" },
        });
    }

    /// <summary>
    /// 发布话题。
    /// </summary>
    public async Task PublishAsync<T>(string topic, T msg)
        => await SendAsync(new { op = "publish", topic, msg });

    /// <summary>
    /// 取消订阅。
    /// </summary>
    public async Task UnsubscribeAsync(string topic)
    {
        _handlers.Remove(topic);
        await SendAsync(new { op = "unsubscribe", topic });
    }

    private void AddHandler(string topic, Action<JsonElement> handler)
    {
        if (!_handlers.TryGetValue(topic, out var list))
            _handlers[topic] = list = new List<Action<JsonElement>>();
        list.Add(handler);
    }

    private void OnMessageReceived(ResponseMessage msg)
    {
        if (msg.MessageType != WebSocketMessageType.Text || string.IsNullOrEmpty(msg.Text))
            return;
        try
        {
            using var doc = JsonDocument.Parse(msg.Text);
            var root = doc.RootElement;
            if (root.TryGetProperty("op", out var op) && op.GetString() == "publish"
                && root.TryGetProperty("topic", out var topicEl)
                && root.TryGetProperty("msg", out var msgEl)
                && _handlers.TryGetValue(topicEl.GetString() ?? "", out var handlers))
            {
                foreach (var h in handlers)
                    h(msgEl);
            }
        }
        catch (JsonException)
        {
            // 忽略无法解析的帧（rosbridge 偶尔会发非 JSON 或状态帧）
        }
    }

    private async Task SendAsync(object payload)
    {
        if (_client is null)
            throw new InvalidOperationException("RosService 未连接，先调用 ConnectAsync");
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        _client.Send(json);
        await Task.CompletedTask;
    }

    private static T Deserialize<T>(JsonElement e) where T : new()
        => e.Deserialize<T>(JsonOpts) ?? new T();

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            _client.Dispose();
            _client = null;
        }
        await Task.CompletedTask;
    }
}
