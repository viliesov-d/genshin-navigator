using System.Diagnostics;

namespace GenshinNavigator.Services;

public sealed class GameProcessMonitor
{
    // Перевіряємо тільки назву процесу. Пам'ять гри та її файли не відкриваються.
    private static readonly string[] KnownProcessNames = ["GenshinImpact", "YuanShen"];

    public bool IsRunning()
    {
        foreach (var name in KnownProcessNames)
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0) return true;
            }
            catch (InvalidOperationException)
            {
                // Процес міг завершитися саме під час перевірки.
            }
        }

        return false;
    }
}
