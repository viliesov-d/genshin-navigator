namespace GenshinNavigator.Models;

public sealed class AppSettings
{
    public double Left { get; set; } = 40;
    public double Top { get; set; } = 120;
    public double Opacity { get; set; } = 0.94;
    public double Scale { get; set; } = 1.0;
    public bool PositionLocked { get; set; }
}
