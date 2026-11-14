using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GenshinNavigator.Models;
using GenshinNavigator.Services;

namespace GenshinNavigator;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly GameProcessMonitor _gameMonitor = new();
    private readonly GameWindowCapture _gameCapture = new();
    private readonly MinimapLocator _minimapLocator = new();
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _trackingTimer = new() { Interval = TimeSpan.FromMilliseconds(1700) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private AppSettings _settings = new();
    private bool _isUpdatingPosition;
    private bool _trackingBusy;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        _gameTimer.Tick += (_, _) => RefreshGameStatus();
        _trackingTimer.Tick += TrackingTimer_Tick;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveSettings(); };
        RefreshGameStatus();
        _gameTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = _settingsStore.Load();
        // У попередній версії панель відкривалась поверх мінікарти за замовчуванням.
        if (string.IsNullOrWhiteSpace(_settings.MapReferencePath) && _settings.Left < 240 && _settings.Top < 220)
            _settings.Left = 280;
        Left = _settings.Left;
        Top = _settings.Top;
        ApplySettings();
        ClampToWorkArea();
        _trackingTimer.Start();
    }

    private void RefreshGameStatus()
    {
        var isRunning = _gameMonitor.IsRunning();
        GameStatus.Text = isRunning ? "Genshin Impact запущена" : "Очікування запуску гри";
        GameProcessDot.Fill = isRunning
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(87, 201, 142))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(104, 115, 129));
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.PositionLocked || e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _settings = dialog.Settings;
        ApplySettings();
        SaveSettings();
        PositionStatus.Text = "Очікування мінікарти";
        PositionDetailStatus.Text = string.IsNullOrWhiteSpace(_settings.MapReferencePath)
            ? "Оберіть скриншот відкритої карти в налаштуваннях"
            : "Зіставлення виконується локально на цьому комп’ютері";
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingPosition || !IsLoaded) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _gameTimer.Stop();
        _trackingTimer.Stop();
        _saveTimer.Stop();
        _isClosing = true;
        if (!_trackingBusy) _minimapLocator.Dispose();
        SaveSettings();
    }

    private async void TrackingTimer_Tick(object? sender, EventArgs e)
    {
        if (_trackingBusy || !IsLoaded) return;
        if (!_gameMonitor.IsRunning())
        {
            PositionStatus.Text = "Очікування запуску гри";
            PositionDetailStatus.Text = "Для аналізу потрібне відкрите вікно гри";
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.MapReferencePath)) return;
        _trackingBusy = true;
        try
        {
            var referencePath = _settings.MapReferencePath;
            var result = await Task.Run(() =>
            {
                using var minimap = _gameCapture.CaptureMinimap(out var message);
                return minimap is null
                    ? MapMatchResult.NotFound(message)
                    : _minimapLocator.Locate(minimap, referencePath);
            });

            if (!IsLoaded) return;
            if (result.Found)
            {
                PositionStatus.Text = $"Знайдено на карті: {result.X:0}, {result.Y:0}";
                PositionDetailStatus.Text = result.Message;
            }
            else
            {
                PositionStatus.Text = "Позицію не зіставлено";
                PositionDetailStatus.Text = result.Message;
            }
        }
        catch (Exception error) when (IsTrackingException(error))
        {
            if (IsLoaded)
            {
                PositionStatus.Text = "Не вдалося визначити позицію";
                PositionDetailStatus.Text = "Перевірте режим вікна гри та скриншот карти";
            }
        }
        finally
        {
            _trackingBusy = false;
            if (_isClosing) _minimapLocator.Dispose();
        }
    }

    private static bool IsTrackingException(Exception error) =>
        error is InvalidOperationException or System.ComponentModel.Win32Exception or OpenCvSharp.OpenCVException or DllNotFoundException;

    private void ApplySettings()
    {
        Card.Opacity = Math.Clamp(_settings.Opacity, 0.2, 1.0);
        var scale = Math.Clamp(_settings.Scale, 0.8, 1.35);
        LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        Cursor = _settings.PositionLocked ? Cursors.Arrow : Cursors.SizeAll;
    }

    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        _isUpdatingPosition = true;
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        _isUpdatingPosition = false;
    }

    private void SaveSettings()
    {
        if (!IsLoaded) return;
        _settings.Left = Left;
        _settings.Top = Top;
        try { _settingsStore.Save(_settings); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
