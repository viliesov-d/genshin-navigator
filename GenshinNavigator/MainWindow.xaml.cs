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
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private AppSettings _settings = new();
    private bool _isUpdatingPosition;

    public MainWindow()
    {
        InitializeComponent();
        _gameTimer.Tick += (_, _) => RefreshGameStatus();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveSettings(); };
        RefreshGameStatus();
        _gameTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = _settingsStore.Load();
        Left = _settings.Left;
        Top = _settings.Top;
        ApplySettings();
        ClampToWorkArea();
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
        _saveTimer.Stop();
        SaveSettings();
    }

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
