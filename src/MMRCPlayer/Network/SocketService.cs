using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MMRCPlayer.Models;

namespace MMRCPlayer.Network;

public class SocketService : IDisposable
{
    private readonly DeviceConfig _config;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _pingCts;
    private bool _isRegistered;
    private bool _disposed;
    
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action<FileState>? OnPlay;
    public event Action<string>? OnStop;
    public event Action? OnPause;
    public event Action? OnResume;
    public event Action? OnRestart;
    public event Action<double>? OnSeek;
    public event Action<int, double?, bool>? OnVolume;
    public event Action<int>? OnPdfPage;
    public event Action<int>? OnPptxPage;
    public event Action<int>? OnFolderPage;
    public event Action? OnPlaceholderRefresh;
    public event Action? OnRegistered;
    public event Action<string>? OnStatusChanged;
    public event Action<FileState>? OnStateSync;
    
    public bool IsConnected => _socket?.State == WebSocketState.Open;
    public bool IsRegistered => _isRegistered;
    
    public SocketService(DeviceConfig config)
    {
        _config = config;
    }
    
    public async Task ConnectAsync()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            
            var serverUrl = _config.ServerUrl;
            if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
                serverUrl = "http://" + serverUrl;
            
            var wsUrl = serverUrl.Replace("http://", "ws://").Replace("https://", "wss://");
            var uri = new Uri(wsUrl);
            
            ShowStatus("Connecting...");
            await _socket.ConnectAsync(uri, _cts.Token);
            
            Log("Socket connected");
            ShowStatus("Connected");
            
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            
            await RegisterDeviceAsync();
            
            StartPingTimer();
            
            OnConnected?.Invoke();
        }
        catch (Exception ex)
        {
            Log($"Connection error: {ex.Message}");
            ShowStatus($"Connection error: {ex.Message}");
            ScheduleReconnect();
        }
    }
    
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        
        try
        {
            while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log("Socket closed by server");
                    ShowStatus("Disconnected. Reconnecting...");
                    OnDisconnected?.Invoke();
                    ScheduleReconnect();
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessageAsync(message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"Receive error: {ex.Message}");
            ShowStatus("Disconnected. Reconnecting...");
            OnDisconnected?.Invoke();
            ScheduleReconnect();
        }
    }
    
    private async Task HandleMessageAsync(string message)
    {
        try
        {
            var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("event", out var eventProp))
            {
                var eventName = eventProp.GetString();
                var data = root.TryGetProperty("data", out var dataProp) ? dataProp : default;
                
                switch (eventName)
                {
                    case "player/registered":
                        _isRegistered = true;
                        OnRegistered?.Invoke();
                        break;
                        
                    case "player/pong":
                        break;
                        
                    case "player/play":
                        if (data.ValueKind != JsonValueKind.Undefined)
                        {
                            var fileState = ParsePlayData(data);
                            OnPlay?.Invoke(fileState);
                        }
                        break;
                        
                    case "player/stop":
                        var reason = "manual_stop";
                        if (data.ValueKind != JsonValueKind.Undefined && 
                            data.TryGetProperty("reason", out var reasonProp))
                            reason = reasonProp.GetString() ?? reason;
                        OnStop?.Invoke(reason);
                        break;
                        
                    case "player/pause":
                        OnPause?.Invoke();
                        break;
                        
                    case "player/resume":
                        OnResume?.Invoke();
                        break;
                        
                    case "player/restart":
                        OnRestart?.Invoke();
                        break;
                        
                    case "player/seek":
                        if (data.ValueKind != JsonValueKind.Undefined)
                        {
                            double position = 0;
                            if (data.ValueKind == JsonValueKind.Number)
                                position = data.GetDouble();
                            else if (data.TryGetProperty("position", out var posProp))
                                position = posProp.GetDouble();
                            OnSeek?.Invoke(position);
                        }
                        break;
                        
                    case "player/volume":
                        if (data.ValueKind != JsonValueKind.Undefined)
                        {
                            int level = 100;
                            bool muted = false;
                            if (data.TryGetProperty("level", out var levelProp))
                                level = levelProp.GetInt32();
                            if (data.TryGetProperty("muted", out var mutedProp))
                                muted = mutedProp.GetBoolean();
                            OnVolume?.Invoke(level, null, muted);
                        }
                        break;
                        
                    case "player/pdfPage":
                        if (data.ValueKind != JsonValueKind.Undefined)
                        {
                            int page = data.ValueKind == JsonValueKind.Number
                                ? data.GetInt32()
                                : int.Parse(data.GetString() ?? "1");
                            OnPdfPage?.Invoke(page);
                        }
                        break;
                        
                    case "player/pptxPage":
                        if (data.ValueKind != JsonValueKind.Undefined)
                        {
                            int slide = data.ValueKind == JsonValueKind.Number
                                ? data.GetInt32()
                                : int.Parse(data.GetString() ?? "1");
                            OnPptxPage?.Invoke(slide);
                        }
                        break;
                        
                    case "player/folderPage":
                        if (data.ValueKind != JsonValueKind.Undefined)
                        {
                            int image = data.ValueKind == JsonValueKind.Number
                                ? data.GetInt32()
                                : int.Parse(data.GetString() ?? "1");
                            OnFolderPage?.Invoke(image);
                        }
                        break;
                        
                    case "placeholder/refresh":
                        OnPlaceholderRefresh?.Invoke();
                        break;
                        
                    case "player/state":
                        if (data.ValueKind != JsonValueKind.Undefined)
                        {
                            var fileState = ParsePlayData(data);
                            if (!string.IsNullOrEmpty(fileState.File))
                                OnStateSync?.Invoke(fileState);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"HandleMessage error: {ex.Message}");
        }
    }
    
    private FileState ParsePlayData(JsonElement data)
    {
        var state = new FileState();
        
        if (data.TryGetProperty("type", out var typeProp))
            state.Type = typeProp.GetString();
        if (data.TryGetProperty("file", out var fileProp))
            state.File = fileProp.GetString();
        if (data.TryGetProperty("page", out var pageProp))
            state.Page = pageProp.GetInt32();
        if (data.TryGetProperty("stream_url", out var streamProp))
            state.StreamUrl = streamProp.GetString();
        if (data.TryGetProperty("stream_protocol", out var protoProp))
            state.StreamProtocol = protoProp.GetString();
        if (data.TryGetProperty("originDeviceId", out var originProp))
            state.OriginDeviceId = originProp.GetString();
        if (data.TryGetProperty("currentTime", out var ctProp))
            state.CurrentTime = ctProp.GetDouble();
        if (data.TryGetProperty("duration", out var durProp))
            state.Duration = durProp.GetDouble();
        
        return state;
    }
    
    private async Task RegisterDeviceAsync()
    {
        if (_socket?.State != WebSocketState.Open) return;
        
        var registration = new
        {
            device_id = _config.DeviceId,
            device_type = "NATIVE_MEDIAPLAYER",
            platform = $"Windows {Environment.OSVersion.VersionString} | MMRC 2.0.0",
            app_version = "2.0.0",
            model = Environment.MachineName,
            manufacturer = "Microsoft",
            capabilities = new
            {
                video = true,
                audio = true,
                images = true,
                pdf = true,
                pptx = true,
                streaming = true
            }
        };
        
        await EmitAsync("player/register", registration);
    }
    
    public async Task EmitAsync(string eventName, object data)
    {
        if (_socket?.State != WebSocketState.Open) return;
        
        var message = JsonSerializer.Serialize(new { @event = eventName, data });
        var bytes = Encoding.UTF8.GetBytes(message);
        
        await _socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }
    
    public async Task SendProgressAsync(string type, string file, double currentTime, double duration, int? page = null, string? streamProtocol = null, string? streamUrl = null)
    {
        var progress = new
        {
            device_id = _config.DeviceId,
            type,
            file,
            currentTime,
            duration,
            page,
            stream_protocol = streamProtocol,
            stream_url = streamUrl
        };
        
        await EmitAsync("player/progress", progress);
    }
    
    private void StartPingTimer()
    {
        StopPingTimer();
        _pingCts = new CancellationTokenSource();
        var token = _pingCts.Token;
        
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.PingIntervalMs, token);
                    if (_socket?.State == WebSocketState.Open && !token.IsCancellationRequested)
                    {
                        await EmitAsync("player/ping", new { device_id = _config.DeviceId });
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, token);
    }
    
    private void StopPingTimer()
    {
        _pingCts?.Cancel();
        _pingCts?.Dispose();
        _pingCts = null;
    }
    
    private void ScheduleReconnect()
    {
        Task.Run(async () =>
        {
            await Task.Delay(_config.ReconnectDelayMs);
            await ConnectAsync();
        });
    }
    
    private void ShowStatus(string message)
    {
        OnStatusChanged?.Invoke(message);
    }
    
    public async Task DisconnectAsync()
    {
        StopPingTimer();
        
        if (_socket != null)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
            _socket.Dispose();
            _socket = null;
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        StopPingTimer();
        _cts?.Cancel();
        
        try { _socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", CancellationToken.None).Wait(1000); } catch { }
        _socket?.Dispose();
        _socket = null;
    }
    
    private void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [Socket] {msg}";
        System.Diagnostics.Debug.WriteLine(line);
    }
}
