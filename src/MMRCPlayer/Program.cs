using System.Runtime.InteropServices;
using MMRCPlayer.Core;
using MMRCPlayer.Rendering;
using MMRCPlayer.Player;
using MMRCPlayer.Network;
using MMRCPlayer.Models;

namespace MMRCPlayer;

public partial class Program
{
    private static IntPtr _hwnd;
    private static D3D11Renderer? _renderer;
    private static OverlayRenderer? _overlay;
    private static LibVLCBindings? _libVLC;
    private static SocketService? _socket;
    private static DeviceConfig? _config;
    private static bool _running = true;
    private static bool _fullscreen = false;
    
    [STAThread]
    static void Main(string[] args)
    {
        Log("Starting MMRC Player v2...");
        
        _config = new DeviceConfig();
        LoadConfig();
        
        var hInstance = NativeWin32.GetModuleHandleW(null);
        
        var wndClass = new NativeWin32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<NativeWin32.WNDCLASSEXW>(),
            style = 3,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate<NativeWin32.WndProcNative>(WndProc),
            hInstance = hInstance,
            hbrBackground = IntPtr.Zero,
            lpszClassName = "MMRCPlayerV2",
        };
        
        NativeWin32.RegisterClassExW(ref wndClass);
        
        var screenWidth = GetSystemMetrics(0);
        var screenHeight = GetSystemMetrics(1);
        
        _hwnd = NativeWin32.CreateWindowExW(
            0,
            "MMRCPlayerV2",
            "MMRC Player v2",
            NativeWin32.WS_POPUP | NativeWin32.WS_VISIBLE,
            0, 0, screenWidth, screenHeight,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        
        if (_hwnd == IntPtr.Zero)
        {
            Log("Failed to create window");
            return;
        }
        
        Log($"Window created: {screenWidth}x{screenHeight}");
        
        _renderer = new D3D11Renderer();
        _renderer.Initialize(_hwnd, screenWidth, screenHeight);
        
        _overlay = new OverlayRenderer();
        _overlay.Initialize(_renderer.Device, screenWidth, screenHeight);
        _overlay.SetStatus("MMRC Player v2.0.0");
        
        _libVLC = new LibVLCBindings();
        _libVLC.Initialize(_renderer.Device, _renderer.DeviceContext, _renderer.SwapChain);
        SetupLibVLCEvents();
        
        _socket = new SocketService(_config);
        SetupSocketEvents();
        _ = Task.Run(() => _socket.ConnectAsync());
        
        var msg = new NativeWin32.MSG();
        while (_running)
        {
            while (NativeWin32.PeekMessageW(out msg, IntPtr.Zero, 0, 0, 1))
            {
                if (msg.message == NativeWin32.WM_DESTROY)
                {
                    _running = false;
                    break;
                }
                
                NativeWin32.TranslateMessage(ref msg);
                NativeWin32.DispatchMessageW(ref msg);
                
                if (msg.message == NativeWin32.WM_KEYDOWN)
                {
                    if (msg.wParam.ToInt32() == NativeWin32.VK_F11)
                    {
                        ToggleFullscreen();
                    }
                    else if (msg.wParam.ToInt32() == NativeWin32.VK_ESCAPE)
                    {
                        _running = false;
                    }
                }
            }
            
            if (!_running) break;
            
            _renderer.Clear(new Vortice.Mathematics.Color4(0, 0, 0, 1));
            
            if (_overlay != null && _libVLC != null && _libVLC.IsPlaying)
            {
                var currentTime = _libVLC.GetCurrentTime();
                var duration = _libVLC.GetDuration();
                _overlay.SetTime($"{FormatTime(currentTime)} / {FormatTime(duration)}");
                _overlay.Render(_renderer.DeviceContext);
            }
            
            _renderer.Present();
            
            Thread.Sleep(1);
        }
        
        _socket?.Dispose();
        _libVLC?.Dispose();
        _overlay?.Dispose();
        _renderer?.Dispose();
        NativeWin32.DestroyWindow(_hwnd);
        
        Log("MMRC Player v2 stopped");
    }
    
    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1
            ? $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
    
    private static void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("server_url", out var serverUrl))
                    _config.ServerUrl = serverUrl.GetString() ?? _config.ServerUrl;
                if (root.TryGetProperty("device_id", out var deviceId))
                    _config.DeviceId = deviceId.GetString() ?? _config.DeviceId;
                if (root.TryGetProperty("ping_interval", out var ping))
                    _config.PingIntervalMs = ping.GetInt32();
                if (root.TryGetProperty("reconnect_delay", out var reconnect))
                    _config.ReconnectDelayMs = reconnect.GetInt32();
                if (root.TryGetProperty("crossfade_duration", out var crossfade))
                    _config.CrossfadeDurationMs = crossfade.GetInt32();
                
                Log($"Config loaded: {_config.DeviceId} -> {_config.ServerUrl}");
            }
        }
        catch (Exception ex)
        {
            Log($"Config load error: {ex.Message}");
        }
    }
    
    private static void SetupLibVLCEvents()
    {
        if (_libVLC == null) return;
        
        _libVLC.OnPlaying += () =>
        {
            Log("LibVLC playing");
            if (_socket?.IsRegistered == true)
            {
                _ = _socket.SendProgressAsync("video", "", _libVLC.GetCurrentTime(), _libVLC.GetDuration());
            }
        };
        
        _libVLC.OnStopped += () =>
        {
            Log("LibVLC stopped");
        };
        
        _libVLC.OnEndReached += () =>
        {
            Log("LibVLC end reached");
        };
        
        _libVLC.OnError += (error) =>
        {
            Log($"LibVLC error: {error}");
        };
    }
    
    private static void SetupSocketEvents()
    {
        if (_socket == null) return;
        
        _socket.OnPlay += (fileState) =>
        {
            Log($"Play received: {fileState.File}");
            _overlay?.SetText(Path.GetFileName(fileState.File));
            
            if (!string.IsNullOrEmpty(fileState.File))
            {
                var url = fileState.File;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    url = "file:///" + url.Replace("\\", "/");
                
                _libVLC?.Play(url);
            }
        };
        
        _socket.OnStop += (reason) =>
        {
            Log($"Stop: {reason}");
            _libVLC?.Stop();
            _overlay?.SetText("");
        };
        
        _socket.OnPause += () =>
        {
            Log("Pause");
            _libVLC?.Pause();
        };
        
        _socket.OnResume += () =>
        {
            Log("Resume");
            _libVLC?.Resume();
        };
        
        _socket.OnSeek += (position) =>
        {
            Log($"Seek: {position}");
            _libVLC?.Seek(position);
        };
        
        _socket.OnVolume += (level, _, muted) =>
        {
            Log($"Volume: {level}, Muted: {muted}");
            _libVLC?.SetVolume(level);
            _libVLC?.SetMute(muted);
        };
        
        _socket.OnRegistered += () =>
        {
            Log("Registered with server");
            _overlay?.SetStatus("Connected to server");
        };
        
        _socket.OnStatusChanged += (status) =>
        {
            Log($"Status: {status}");
            _overlay?.SetStatus(status);
        };
    }
    
    private static void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        
        if (_fullscreen)
        {
            var screenWidth = GetSystemMetrics(0);
            var screenHeight = GetSystemMetrics(1);
            
            NativeWin32.SetWindowLongW(_hwnd, NativeWin32.GWL_STYLE, 
                NativeWin32.WS_POPUP | NativeWin32.WS_VISIBLE);
            NativeWin32.SetWindowPos(_hwnd, IntPtr.Zero, 
                0, 0, screenWidth, screenHeight, 
                NativeWin32.SWP_FRAMECHANGED | NativeWin32.SWP_SHOWWINDOW);
            
            _renderer?.Resize(screenWidth, screenHeight);
            Log("Entered fullscreen");
        }
        else
        {
            var width = 1280;
            var height = 720;
            var x = (GetSystemMetrics(0) - width) / 2;
            var y = (GetSystemMetrics(1) - height) / 2;
            
            NativeWin32.SetWindowLongW(_hwnd, NativeWin32.GWL_STYLE, 
                0x00CF0000);
            NativeWin32.SetWindowPos(_hwnd, IntPtr.Zero, 
                x, y, width, height, 
                NativeWin32.SWP_FRAMECHANGED | NativeWin32.SWP_SHOWWINDOW);
            
            _renderer?.Resize(width, height);
            Log("Exited fullscreen");
        }
    }
    
    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeWin32.WM_SIZE:
                if (_renderer != null && wParam.ToInt32() != 0)
                {
                    var width = lParam.ToInt32() & 0xFFFF;
                    var height = (lParam.ToInt32() >> 16) & 0xFFFF;
                    _renderer.Resize(width, height);
                    _overlay?.Resize(width, height);
                }
                break;
                
            case NativeWin32.WM_DESTROY:
                _running = false;
                return IntPtr.Zero;
        }
        
        return NativeWin32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }
    
    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);
    
    private static void Log(string msg)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [Main] {msg}";
            System.Diagnostics.Debug.WriteLine(line);
        }
        catch { }
    }
}
