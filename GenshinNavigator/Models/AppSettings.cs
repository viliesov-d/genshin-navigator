namespace GenshinNavigator.Models;

public sealed class AppSettings
{
    // Залишаємо верхній лівий кут вільним для мінікарти.
    public double Left { get; set; } = 280;
    public double Top { get; set; } = 120;
    public double Opacity { get; set; } = 0.94;
    public double Scale { get; set; } = 1.0;
    public bool PositionLocked { get; set; }
    public string MapReferencePath { get; set; } = string.Empty;
}
