using System.Windows;
using GenshinNavigator.Models;

namespace GenshinNavigator;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        Settings = new AppSettings
        {
            Left = current.Left,
            Top = current.Top,
            Opacity = current.Opacity,
            Scale = current.Scale,
            PositionLocked = current.PositionLocked
        };
        OpacitySlider.Value = Math.Clamp(Settings.Opacity * 100, 20, 100);
        ScaleSlider.Value = Math.Clamp(Settings.Scale * 100, 80, 135);
        LockPositionCheckBox.IsChecked = Settings.PositionLocked;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValue is not null) OpacityValue.Text = $"{(int)e.NewValue}%";
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValue is not null) ScaleValue.Text = $"{(int)e.NewValue}%";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings.Opacity = OpacitySlider.Value / 100;
        Settings.Scale = ScaleSlider.Value / 100;
        Settings.PositionLocked = LockPositionCheckBox.IsChecked == true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
