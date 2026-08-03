using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MMRCPlayer.Rendering;

public class D3D11Renderer : IDisposable
{
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _deviceContext;
    private IDXGISwapChain? _swapChain;
    private ID3D11RenderTargetView? _renderTargetView;
    private bool _disposed;
    
    public ID3D11Device Device => _device ?? throw new InvalidOperationException("Device not initialized");
    public ID3D11DeviceContext DeviceContext => _deviceContext ?? throw new InvalidOperationException("DeviceContext not initialized");
    public IDXGISwapChain SwapChain => _swapChain ?? throw new InvalidOperationException("SwapChain not initialized");
    
    public void Initialize(IntPtr hwnd, int width, int height)
    {
        var featureLevels = new FeatureLevel[]
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0,
        };
        
        var createFlags = DeviceCreationFlags.BgraSupport;
        
        var swapChainDesc = new SwapChainDescription
        {
            BufferCount = 2,
            BufferDescription = new ModeDescription(width, height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
            BufferUsage = Usage.RenderTargetOutput,
            OutputWindow = hwnd,
            SampleDescription = new SampleDescription(1, 0),
            Windowed = true,
            SwapEffect = SwapEffect.FlipDiscard,
            Flags = SwapChainFlags.None,
        };
        
        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            createFlags,
            featureLevels,
            out _device,
            out _deviceContext,
            out var swapChain);
        
        _swapChain = swapChain.QueryInterface<IDXGISwapChain>();
        
        CreateRenderTarget();
        
        Log($"D3D11 initialized: {_device.Description.Description}");
        Log($"Feature Level: {_device.FeatureLevel}");
    }
    
    private void CreateRenderTarget()
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device!.CreateRenderTargetView(backBuffer);
    }
    
    public void Resize(int width, int height)
    {
        if (_disposed || _swapChain == null) return;
        
        _renderTargetView?.Dispose();
        _renderTargetView = null;
        
        _swapChain.ResizeBuffers(2, width, height, Format.Unknown, SwapChainFlags.None);
        
        CreateRenderTarget();
    }
    
    public void Clear(Color4 color)
    {
        if (_disposed || _deviceContext == null || _renderTargetView == null) return;
        
        _deviceContext.ClearRenderTargetView(_renderTargetView, color);
        _deviceContext.OMSetRenderTargets(_renderTargetView);
    }
    
    public void Present()
    {
        if (_disposed || _swapChain == null) return;
        
        try
        {
            _swapChain.Present(1, PresentFlags.None);
        }
        catch (SharpGen.Runtime.SharpGenException ex)
        {
            Log($"Present error: {ex.Message}");
            HandleDeviceLost();
        }
    }
    
    private void HandleDeviceLost()
    {
        Log("Device lost, attempting recovery...");
        
        _renderTargetView?.Dispose();
        _renderTargetView = null;
        
        _deviceContext?.Dispose();
        _deviceContext = null;
        
        _device?.Dispose();
        _device = null;
        
        _swapChain?.Dispose();
        _swapChain = null;
        
        Log("Device lost recovery not implemented yet");
    }
    
    public IntPtr GetSwapChainHandle()
    {
        if (_swapChain == null) return IntPtr.Zero;
        
        var nativePointer = _swapChain.NativePointer;
        return nativePointer;
    }
    
    private static void Log(string msg)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [D3D11] {msg}";
            System.Diagnostics.Debug.WriteLine(line);
        }
        catch { }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _renderTargetView?.Dispose();
        _deviceContext?.Dispose();
        _device?.Dispose();
        _swapChain?.Dispose();
    }
}
