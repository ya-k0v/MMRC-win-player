using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.Direct3D11;

namespace MMRCPlayer.Rendering;

public class OverlayRenderer : IDisposable
{
    private ID2D1Factory? _d2dFactory;
    private ID2D1RenderTarget? _d2dRenderTarget;
    private IDWriteFactory? _dwriteFactory;
    private IDWriteTextFormat? _textFormat;
    private ID2D1SolidColorBrush? _textBrush;
    private ID2D1SolidColorBrush? _backgroundBrush;
    private ID3D11Texture2D? _sharedTexture;
    private IDXGISurface? _dxgiSurface;
    private bool _disposed;
    private int _width;
    private int _height;
    
    private string _currentText = "";
    private string _statusText = "";
    private string _timeText = "";
    
    public OverlayRenderer()
    {
    }
    
    public bool Initialize(ID3D11Device device, int width, int height)
    {
        try
        {
            _width = width;
            _height = height;
            
            _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory>();
            _dwriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
            
            _textFormat = _dwriteFactory.CreateTextFormat(
                "Segoe UI",
                FontWeight.Normal,
                FontStyle.Normal,
                FontStretch.Normal,
                24.0f
            );
            
            var textureDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                MiscFlags = ResourceOptionFlags.Shared
            };
            
            _sharedTexture = device.CreateTexture2D(textureDesc);
            _dxgiSurface = _sharedTexture.QueryInterface<IDXGISurface>();
            
            var renderTargetProperties = new RenderTargetProperties();
            var bitmapProperties = new BitmapProperties(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)
            );
            
            _d2dRenderTarget = _d2dFactory.CreateDxgiSurfaceRenderTarget(
                _dxgiSurface,
                renderTargetProperties,
                bitmapProperties
            );
            
            _textBrush = _d2dRenderTarget.CreateSolidColorBrush(new Color4(1.0f, 1.0f, 1.0f, 1.0f));
            _backgroundBrush = _d2dRenderTarget.CreateSolidColorBrush(new Color4(0.0f, 0.0f, 0.0f, 0.7f));
            
            Log("Overlay renderer initialized");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Overlay initialization failed: {ex.Message}");
            return false;
        }
    }
    
    public void Resize(int width, int height)
    {
        _width = width;
        _height = height;
    }
    
    public void SetText(string text)
    {
        _currentText = text;
    }
    
    public void SetStatus(string status)
    {
        _statusText = status;
    }
    
    public void SetTime(string time)
    {
        _timeText = time;
    }
    
    public void Render(ID3D11DeviceContext deviceContext)
    {
        if (_d2dRenderTarget == null || _textBrush == null || _backgroundBrush == null)
            return;
        
        try
        {
            _d2dRenderTarget.BeginDraw();
            _d2dRenderTarget.Clear(new Color4(0, 0, 0, 0));
            
            if (!string.IsNullOrEmpty(_currentText))
            {
                var textRect = new Rect2(0, _height / 2 - 50, _width, 100);
                _textFormat.TextAlignment = TextAlignment.Center;
                _textFormat.ParagraphAlignment = ParagraphAlignment.Center;
                _d2dRenderTarget.DrawText(_currentText, _textFormat, textRect, _textBrush);
            }
            
            if (!string.IsNullOrEmpty(_statusText))
            {
                var statusRect = new Rect2(20, 20, 400, 40);
                _textFormat.TextAlignment = TextAlignment.Leading;
                _textFormat.ParagraphAlignment = ParagraphAlignment.Near;
                _d2dRenderTarget.DrawText(_statusText, _textFormat, statusRect, _textBrush);
            }
            
            if (!string.IsNullOrEmpty(_timeText))
            {
                var timeRect = new Rect2(_width - 220, _height - 60, 200, 40);
                _textFormat.TextAlignment = TextAlignment.Trailing;
                _textFormat.ParagraphAlignment = ParagraphAlignment.Far;
                _d2dRenderTarget.DrawText(_timeText, _textFormat, timeRect, _textBrush);
            }
            
            _d2dRenderTarget.EndDraw();
        }
        catch (Exception ex)
        {
            Log($"Overlay render error: {ex.Message}");
        }
    }
    
    public ID3D11Texture2D? GetSharedTexture()
    {
        return _sharedTexture;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _textBrush?.Dispose();
        _backgroundBrush?.Dispose();
        _textFormat?.Dispose();
        _d2dRenderTarget?.Dispose();
        _dwriteFactory?.Dispose();
        _d2dFactory?.Dispose();
        _dxgiSurface?.Dispose();
        _sharedTexture?.Dispose();
    }
    
    private static void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [Overlay] {msg}";
        System.Diagnostics.Debug.WriteLine(line);
    }
}
