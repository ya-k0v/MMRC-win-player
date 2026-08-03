namespace MMRCPlayer.Models;

public class DeviceConfig
{
    public string ServerUrl { get; set; } = "http://localhost:3000";
    public string DeviceId { get; set; } = "win-001";
    public bool ShowStatus { get; set; }
    public bool Fullscreen { get; set; }
    public int PingIntervalMs { get; set; } = 20000;
    public int ReconnectDelayMs { get; set; } = 5000;
    public int CrossfadeDurationMs { get; set; } = 500;
}
