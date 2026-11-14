using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace GenshinNavigator.Services;

/// <summary>Знімає лише видимі пікселі ігрового вікна; пам'ять і файли гри не читаються.</summary>
public sealed class GameWindowCapture
{
    private static readonly string[] ProcessNames = ["GenshinImpact", "YuanShen"];

    public Mat? CaptureMinimap(out string message)
    {
        foreach (var processName in ProcessNames)
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(processName); }
            catch (InvalidOperationException) { continue; }

            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        var window = process.MainWindowHandle;
                        if (window == IntPtr.Zero || !GetClientRect(window, out var client)) continue;

                        var width = client.Right - client.Left;
                        var height = client.Bottom - client.Top;
                        if (width < 600 || height < 400) continue;

                        var origin = new NativePoint { X = 0, Y = 0 };
                        if (!ClientToScreen(window, ref origin)) continue;

                        // Координати задані відносно висоти, щоб працювати на різних 16:9 роздільностях.
                        var centerX = (int)Math.Round(height * 0.133);
                        var centerY = (int)Math.Round(height * 0.105);
                        var radius = (int)Math.Round(height * 0.078);
                        var x = Math.Clamp(centerX - radius, 0, width - 1);
                        var y = Math.Clamp(centerY - radius, 0, height - 1);
                        var side = Math.Min(radius * 2, Math.Min(width - x, height - y));
                        if (side < 100) continue;

                        // Захоплюємо тільки ділянку мінікарти, а не весь екран гри.
                        using var bitmap = new Bitmap(side, side, PixelFormat.Format24bppRgb);
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen(origin.X + x, origin.Y + y, 0, 0,
                                new System.Drawing.Size(side, side), CopyPixelOperation.SourceCopy);
                        }
                        using var stream = new MemoryStream();
                        bitmap.Save(stream, ImageFormat.Png);
                        using var minimap = Cv2.ImDecode(stream.ToArray(), ImreadModes.Color);
                        if (minimap.Empty()) continue;

                        message = "Мінікарту захоплено локально";
                        return minimap.Clone();
                    }
                    catch (Exception ex) when (ex is ArgumentException or ExternalException or InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // Гра могла згортатися чи змінювати розмір під час знімка.
                    }
                }
            }
        }

        message = "Не вдалося захопити мінікарту. Перевірте, що гра відкрита у віконному або borderless режимі.";
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);
}
