using System.Windows;
using Microsoft.Win32;
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
            PositionLocked = current.PositionLocked,
            MapReferencePath = current.MapReferencePath
        };
        OpacitySlider.Value = Math.Clamp(Settings.Opacity * 100, 20, 100);
        ScaleSlider.Value = Math.Clamp(Settings.Scale * 100, 80, 135);
        LockPositionCheckBox.IsChecked = Settings.PositionLocked;
        MapPathTextBox.Text = Settings.MapReferencePath;
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
        Settings.MapReferencePath = MapPathTextBox.Text;
        DialogResult = true;
    }

    private void BrowseMap_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Оберіть скриншот карти Genshin Impact",
            Filter = "Зображення|*.png;*.jpg;*.jpeg;*.bmp|Усі файли|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        MapPathTextBox.Text = dialog.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
