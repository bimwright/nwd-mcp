using System.Windows;
using System.Windows.Controls;
using Bimwright.Nwd.Shared.Config;
using Bimwright.Nwd.Shared.Localization;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// Small status window. Toast and branding apply immediately. Idle needs Apply.
/// </summary>
internal sealed class NwdToastStatusWindow : Window
{
    private readonly CheckBox _toastEnabled;
    private readonly CheckBox _showBranding;
    private readonly CheckBox _recordCalls;
    private readonly ComboBox _idle;
    private readonly Button _apply;
    private int _savedIdle;
    private bool _ready;

    public NwdToastStatusWindow()
    {
        Title = L.T("toast.status.window");
        Width = 360;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5));
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;

        _savedIdle = PluginSettings.LoadToastIdleSeconds();
        _toastEnabled = new CheckBox { Content = L.T("settings.toast.enabled"), Margin = new Thickness(0, 0, 0, 4) };
        _showBranding = new CheckBox { Content = L.T("settings.toast.brand"), Margin = new Thickness(0, 8, 0, 4) };
        _recordCalls = new CheckBox { Content = L.T("settings.record"), Margin = new Thickness(0, 8, 0, 4) };
        _idle = new ComboBox { Margin = new Thickness(0, 4, 0, 8), IsEditable = false };
        foreach (var seconds in PluginSettings.ToastIdleChoices)
        {
            var item = new ComboBoxItem
            {
                Content = L.T("settings.toast.idle.seconds", ("seconds", seconds)),
                Tag = seconds
            };
            _idle.Items.Add(item);
            if (seconds == _savedIdle)
                _idle.SelectedItem = item;
        }

        _apply = new Button { Content = L.T("settings.apply"), Width = 88, Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
        var close = new Button { Content = L.T("settings.close"), Width = 88, IsCancel = true };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(_apply);
        buttons.Children.Add(close);

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(_toastEnabled);
        root.Children.Add(Help(L.T("settings.toast.enabled.help")));
        root.Children.Add(_showBranding);
        root.Children.Add(Help(L.T("settings.toast.brand.help")));
        root.Children.Add(_recordCalls);
        root.Children.Add(Help(L.T("settings.record.help")));
        root.Children.Add(new TextBlock { Text = L.T("settings.toast.idle"), Margin = new Thickness(0, 8, 0, 0) });
        root.Children.Add(_idle);
        root.Children.Add(Help(L.T("settings.toast.idle.help")));
        root.Children.Add(buttons);
        Content = root;

        _toastEnabled.IsChecked = NwdActivityToast.Enabled;
        _showBranding.IsChecked = NwdActivityToast.ShowBranding;
        _recordCalls.IsChecked = NwdActivityToast.RecordCalls;
        SyncBrandEnabled();
        _ready = true;

        _toastEnabled.Checked += (_, _) => OnToastToggled();
        _toastEnabled.Unchecked += (_, _) => OnToastToggled();
        _showBranding.Checked += (_, _) => OnBrandToggled();
        _showBranding.Unchecked += (_, _) => OnBrandToggled();
        _recordCalls.Checked += (_, _) => OnRecordToggled();
        _recordCalls.Unchecked += (_, _) => OnRecordToggled();
        _idle.SelectionChanged += (_, _) => SyncApply();
        _apply.Click += (_, _) => ApplyIdle();
        close.Click += (_, _) => Close();
        Closing += OnClosing;
    }

    private static TextBlock Help(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private void OnToastToggled()
    {
        if (!_ready)
            return;
        NwdActivityToast.ApplyEnabled(_toastEnabled.IsChecked == true);
        SyncBrandEnabled();
    }

    private void OnBrandToggled()
    {
        if (!_ready || !_showBranding.IsEnabled)
            return;
        NwdActivityToast.ApplyBranding(_showBranding.IsChecked == true);
    }

    private void OnRecordToggled()
    {
        if (!_ready)
            return;
        NwdActivityToast.ApplyRecord(_recordCalls.IsChecked == true);
    }

    private void SyncBrandEnabled()
    {
        _showBranding.IsEnabled = _toastEnabled.IsChecked == true;
    }

    private void SyncApply()
    {
        _apply.IsEnabled = SelectedIdle() != _savedIdle;
    }

    private int SelectedIdle()
    {
        return _idle.SelectedItem is ComboBoxItem item && item.Tag is int seconds
            ? seconds
            : _savedIdle;
    }

    private void ApplyIdle()
    {
        var seconds = SelectedIdle();
        if (!PluginSettings.SaveToastIdleSeconds(seconds))
            return;
        _savedIdle = PluginSettings.NormalizeToastIdleSeconds(seconds);
        NwdActivityToast.SetIdleSeconds(_savedIdle);
        SyncApply();
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (SelectedIdle() == _savedIdle)
            return;
        var keep = MessageBox.Show(
            this,
            L.T("settings.discard"),
            Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (keep != MessageBoxResult.Yes)
            e.Cancel = true;
    }
}
