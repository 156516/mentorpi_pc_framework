// ROS2 客户端服务
// 用 rosbridge (WebSocket) 跨平台，直接实现 rosbridge JSON 协议（不依赖原生 rcl）

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
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
    // call_service 按 id 匹配 service_response 的回调；调完即销毁
    private readonly Dictionary<string, TaskCompletionSource<ServiceResponse>> _pendingCalls = new();

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

    /// <summary>
    /// 调 ROS service（通过 rosbridge op:"call_service"）。
    /// args 是 service request 的 JSON 表示（如 new { name = new { data = "/workspace/maps/room1" } }）。
    /// 返回 (result, values)：result=true 成功，values 是反序列化后的 TResp 对象（失败时为 null）。
    /// 默认超时 10s。
    /// </summary>
    public async Task<(bool Result, TResp? Values)> CallServiceAsync<TResp>(
        string service, object args, CancellationToken ct = default)
        where TResp : class, new()
    {
        var id = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<ServiceResponse>();
        _pendingCalls[id] = tcs;

        try
        {
            await SendAsync(new
            {
                op = "call_service",
                service,
                id,
                args,
            });

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            var resp = await tcs.Task.WaitAsync(timeoutCts.Token);

            TResp? values = null;
            if (resp.Values.HasValue)
            {
                try { values = resp.Values.Value.Deserialize<TResp>(JsonOpts); }
                catch (JsonException) { /* 留 null */ }
            }
            return (resp.Result, values);
        }
        finally
        {
            _pendingCalls.Remove(id);
        }
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
            var op = root.TryGetProperty("op", out var opEl) ? opEl.GetString() : null;

            switch (op)
            {
                case "publish":
                    HandlePublish(root);
                    break;
                case "service_response":
                    HandleServiceResponse(root);
                    break;
                // 其它 op（status / pong / fragment 等）忽略
            }
        }
        catch (JsonException)
        {
            // 忽略无法解析的帧（rosbridge 偶尔会发非 JSON 或状态帧）
        }
    }

    private void HandlePublish(JsonElement root)
    {
        if (!root.TryGetProperty("topic", out var topicEl)
            || !root.TryGetProperty("msg", out var msgEl))
            return;
        if (_handlers.TryGetValue(topicEl.GetString() ?? "", out var handlers))
        {
            foreach (var h in handlers)
                h(msgEl);
        }
    }

    private void HandleServiceResponse(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idEl))
            return;
        var id = idEl.GetString();
        if (id is null || !_pendingCalls.TryGetValue(id, out var tcs))
            return;
        var result = root.TryGetProperty("result", out var rEl) && rEl.ValueKind == JsonValueKind.True;
        JsonElement values = default;
        var hasValues = root.TryGetProperty("values", out var vEl);
        if (hasValues) values = vEl.Clone();
        tcs.TrySetResult(new ServiceResponse(result, hasValues ? values : null));
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
        // 所有 pending call 取消
        foreach (var tcs in _pendingCalls.Values)
            tcs.TrySetCanceled();
        _pendingCalls.Clear();
        await Task.CompletedTask;
    }

    private readonly record struct ServiceResponse(bool Result, JsonElement? Values);
}