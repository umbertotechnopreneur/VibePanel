// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
namespace VibePanel.Studio;

public sealed record StudioPreferences
{
    public int RefreshMilliseconds { get; set; } = 1000;
    public string JpegQuality { get; set; } = "high";
    public string ProfileSelection { get; set; } = "auto";
    public long LastChange { get; set; } = Environment.TickCount64;
    public string SelectedCategory { get; set; } = "multimedia";
    public string SelectedTheme { get; set; } = "";
    public string SelectedProfile { get; set; } = "wide-1280x480";
    public string FontFamily { get; set; } = "Cascadia Mono";
    public int Appearance { get; set; } = 0;
    public double FontScale { get; set; } = 1;
    public double SaturationPercent { get; set; } = 100;
    public double HueDegrees { get; set; } = 0;
    public double BrightnessPercent { get; set; } = 0;
    public double ContrastPercent { get; set; } = 100;
    public bool ColorFiltersEnabled { get; set; } = false;
    public bool CycleThemes { get; set; } = false;
    public string? SelectedDeviceId { get; set; } = null;
    public bool ShowSpectrum { get; set; } = true;
    public bool Light => Appearance == 2 || Appearance == 0 && DateTime.Now.Hour is >= 7 and < 19;
}
