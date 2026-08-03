using System.Runtime.InteropServices;
using Vortice.Direct3D11;

namespace MMRCPlayer.Player;

public class LibVLCBindings : IDisposable
{
    private IntPtr _libVLC;
    private IntPtr _mediaPlayer;
    private IntPtr _media;
    private bool _disposed;
    
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    
    public event Action? OnPlaying;
    public event Action? OnStopped;
    public event Action? OnEndReached;
    public event Action<string>? OnError;
    
    public LibVLCBindings()
    {
    }
    
    public void Initialize(ID3D11Device device, ID3D11DeviceContext deviceContext, Vortice.DXGI.IDXGISwapChain swapChain)
    {
        _libVLC = LibVLCPInvoke.libvlc_new(0, IntPtr.Zero);
        
        if (_libVLC == IntPtr.Zero)
            throw new Exception("Failed to initialize LibVLC");
        
        Log("LibVLC initialized");
    }
    
    public void Play(string url)
    {
        if (_libVLC == IntPtr.Zero)
            throw new Exception("LibVLC not initialized");
        
        Stop();
        
        _media = LibVLCPInvoke.libvlc_media_new_location(_libVLC, url);
        _mediaPlayer = LibVLCPInvoke.libvlc_media_player_new_from_media(_media);
        
        SetupCallbacks();
        
        LibVLCPInvoke.libvlc_media_player_play(_mediaPlayer);
        IsPlaying = true;
        IsPaused = false;
        
        Log($"Playing: {url}");
    }
    
    private void SetupCallbacks()
    {
        if (_mediaPlayer == IntPtr.Zero) return;
    }
    
    public void Stop()
    {
        if (_mediaPlayer == IntPtr.Zero) return;
        
        LibVLCPInvoke.libvlc_media_player_stop(_mediaPlayer);
        IsPlaying = false;
        IsPaused = false;
        
        LibVLCPInvoke.libvlc_media_player_release(_mediaPlayer);
        _mediaPlayer = IntPtr.Zero;
        
        if (_media != IntPtr.Zero)
        {
            LibVLCPInvoke.libvlc_media_release(_media);
            _media = IntPtr.Zero;
        }
        
        Log("Stopped");
    }
    
    public void Pause()
    {
        if (_mediaPlayer == IntPtr.Zero) return;
        
        LibVLCPInvoke.libvlc_media_player_set_pause(_mediaPlayer, 1);
        IsPaused = true;
        
        Log("Paused");
    }
    
    public void Resume()
    {
        if (_mediaPlayer == IntPtr.Zero) return;
        
        LibVLCPInvoke.libvlc_media_player_set_pause(_mediaPlayer, 0);
        IsPaused = false;
        
        Log("Resumed");
    }
    
    public void SetVolume(int level)
    {
        if (_mediaPlayer == IntPtr.Zero) return;
        
        LibVLCPInvoke.libvlc_audio_set_volume(_mediaPlayer, Math.Clamp(level, 0, 100));
    }
    
    public void SetMute(bool muted)
    {
        if (_mediaPlayer == IntPtr.Zero) return;
        
        LibVLCPInvoke.libvlc_audio_set_mute(_mediaPlayer, muted ? 1 : 0);
    }
    
    public void Seek(double position)
    {
        if (_mediaPlayer == IntPtr.Zero) return;
        
        LibVLCPInvoke.libvlc_media_player_set_position(_mediaPlayer, (float)position);
    }
    
    public double GetCurrentTime()
    {
        if (_mediaPlayer == IntPtr.Zero) return 0;
        
        return LibVLCPInvoke.libvlc_media_player_get_time(_mediaPlayer) / 1000.0;
    }
    
    public double GetDuration()
    {
        if (_mediaPlayer == IntPtr.Zero) return 0;
        
        return LibVLCPInvoke.libvlc_media_player_get_length(_mediaPlayer) / 1000.0;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
        
        if (_libVLC != IntPtr.Zero)
        {
            LibVLCPInvoke.libvlc_release(_libVLC);
            _libVLC = IntPtr.Zero;
        }
    }
    
    private static void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [LibVLC] {msg}";
        System.Diagnostics.Debug.WriteLine(line);
    }
}

public static class LibVLCPInvoke
{
    private const string LibVLC = "libvlc";
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libvlc_new(int argc, IntPtr argv);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_release(IntPtr libVLC);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libvlc_media_new_location(IntPtr libVLC, [MarshalAs(UnmanagedType.LPStr)] string url);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_media_release(IntPtr media);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_media_player_release(IntPtr mediaPlayer);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_media_player_play(IntPtr mediaPlayer);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_media_player_stop(IntPtr mediaPlayer);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_media_player_set_pause(IntPtr mediaPlayer, int doPause);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_media_player_set_position(IntPtr mediaPlayer, float position);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern float libvlc_media_player_get_position(IntPtr mediaPlayer);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern long libvlc_media_player_get_time(IntPtr mediaPlayer);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern long libvlc_media_player_get_length(IntPtr mediaPlayer);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libvlc_audio_set_volume(IntPtr mediaPlayer, int volume);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libvlc_audio_set_mute(IntPtr mediaPlayer, int mute);
    
    [DllImport(LibVLC, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libvlc_media_player_get_state(IntPtr mediaPlayer);
}
