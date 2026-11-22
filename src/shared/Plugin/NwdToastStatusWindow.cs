using System.Windows;
using System.Windows.Controls;
using Bimwright.Nwd.Shared.Config;
using Bimwright.Nwd.Shared.Localization;
using Bimwright.Nwd.Shared.Views.Toast;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// Status/preferences window. Toast, pause, branding and position apply immediately. Idle needs Apply.
/// </summary>
internal sealed class NwdToastStatusWindow : Window
{
    private readonly CheckBox _toastEnabled;
    private readonly CheckBox _paused;
    private readonly TextBlock _pausedError;
    private readonly System.Action _pauseChangedHandler;
    private readonly CheckBox _showBranding;
    private readonly CheckBox _recordCalls;
    private readonly ComboBox _idle;
    private readonly TextBlock _idleError;
    private readonly ComboBox _horizontal;
    private readonly ComboBox _vertical;
    private readonly CheckBox _dragEnabled;
    private readonly Button _resetPosition;
    private readonly TextBlock _positionError;
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
        _paused = new CheckBox { Content = L.T("settings.toast.snooze"), Margin = new Thickness(0, 8, 0, 4) };
        _pausedError = Help(L.T("toast.status.saveFailed"));
        _pausedError.Foreground = System.Windows.Media.Brushes.Firebrick;
        _pausedError.Visibility = Visibility.Collapsed;
        _showBranding = new CheckBox { Content = L.T("settings.toast.brand"), Margin = new Thickness(0, 8, 0, 4) };
        _recordCalls = new CheckBox { Content = L.T("settings.record"), Margin = new Thickness(0, 8, 0, 4) };
        _idle = new ComboBox { Margin = new Thickness(0, 4, 0, 8), IsEditable = false };
        _idleError = Help(string.Empty);
        _idleError.Foreground = System.Windows.Media.Brushes.Firebrick;
        _idleError.Visibility = Visibility.Collapsed;
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

        _horizontal = PositionChoice("settings.toast.left", "settings.toast.right");
        _vertical = PositionChoice("settings.toast.top", "settings.toast.bottom");
        _dragEnabled = new CheckBox { Content = L.T("settings.toast.drag"), Margin = new Thickness(0, 8, 0, 4) };
        _resetPosition = new Button { Content = L.T("settings.toast.reset"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) };
        _positionError = Help(string.Empty);
        _positionError.Foreground = System.Windows.Media.Brushes.Firebrick;
        _positionError.Visibility = Visibility.Collapsed;

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
        var history = new Button { Content = "History", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 4) };
        history.Click += (_, _) => NwdActivityToast.ShowHistoryWindow();
        root.Children.Add(history);
        root.Children.Add(Help(L.T("settings.toast.enabled.help")));
        root.Children.Add(_paused);
        root.Children.Add(Help(L.T("settings.toast.snooze.help")));
        root.Children.Add(_pausedError);
        root.Children.Add(_showBranding);
        root.Children.Add(Help(L.T("settings.toast.brand.help")));
        root.Children.Add(_recordCalls);
        root.Children.Add(Help(L.T("settings.record.help")));
        root.Children.Add(new TextBlock { Text = L.T("settings.toast.idle"), Margin = new Thickness(0, 8, 0, 0) });
        root.Children.Add(_idle);
        root.Children.Add(_idleError);
        root.Children.Add(Help(L.T("settings.toast.idle.help")));
        root.Children.Add(new TextBlock { Text = L.T("settings.toast.horizontal"), Margin = new Thickness(0, 8, 0, 0) });
        root.Children.Add(_horizontal);
        root.Children.Add(new TextBlock { Text = L.T("settings.toast.vertical"), Margin = new Thickness(0, 4, 0, 0) });
        root.Children.Add(_vertical);
        root.Children.Add(Help(L.T("settings.toast.position.help")));
        root.Children.Add(_dragEnabled);
        root.Children.Add(Help(L.T("settings.toast.drag.help")));
        root.Children.Add(_resetPosition);
        root.Children.Add(_positionError);
        root.Children.Add(buttons);
        Content = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = System.Math.Max(240, SystemParameters.WorkArea.Height - 80)
        };

        _toastEnabled.IsChecked = NwdActivityToast.Enabled;
        _showBranding.IsChecked = NwdActivityToast.ShowBranding;
        _recordCalls.IsChecked = NwdActivityToast.RecordCalls;
        SyncBrandEnabled();
        SyncPaused();
        SyncPosition(true);
        _ready = true;

        _toastEnabled.Checked += (_, _) => OnToastToggled();
        _toastEnabled.Unchecked += (_, _) => OnToastToggled();
        _paused.Checked += (_, _) => OnPauseToggled();
        _paused.Unchecked += (_, _) => OnPauseToggled();
        // A pause from the card (or Resume) in this Navisworks updates the switch while the window is open.
        _pauseChangedHandler = () =>
        {
            if (Dispatcher.HasShutdownStarted)
                return;
            if (Dispatcher.CheckAccess()) SyncPaused();
            else Dispatcher.BeginInvoke(new System.Action(SyncPaused));
        };
        ToastSnooze.Changed += _pauseChangedHandler;
        Closed += (_, _) => ToastSnooze.Changed -= _pauseChangedHandler;
        _showBranding.Checked += (_, _) => OnBrandToggled();
        _showBranding.Unchecked += (_, _) => OnBrandToggled();
        _recordCalls.Checked += (_, _) => OnRecordToggled();
        _recordCalls.Unchecked += (_, _) => OnRecordToggled();
        _idle.SelectionChanged += (_, _) => SyncApply();
        _horizontal.SelectionChanged += (_, _) => OnCornerChanged();
        _vertical.SelectionChanged += (_, _) => OnCornerChanged();
        _dragEnabled.Checked += (_, _) => OnDragChanged();
        _dragEnabled.Unchecked += (_, _) => OnDragChanged();
        _resetPosition.Click += (_, _) => ApplyPosition(NwdActivityToast.PositionOptions.WithOffset(null, null));
        _apply.Click += (_, _) => ApplyIdle();
        close.Click += (_, _) => Close();
        Closing += OnClosing;
    }

    private static ComboBox PositionChoice(string first, string second)
    {
        var choice = new ComboBox { Margin = new Thickness(0, 4, 0, 4), IsEditable = false };
        choice.Items.Add(new ComboBoxItem { Content = L.T(first), Tag = false });
        choice.Items.Add(new ComboBoxItem { Content = L.T(second), Tag = true });
        return choice;
    }

    internal void SyncPosition(bool saved)
    {
        var ready = _ready;
        _ready = false;
        var options = NwdActivityToast.PositionOptions;
        _horizontal.SelectedIndex = options.Right ? 1 : 0;
        _vertical.SelectedIndex = options.Bottom ? 1 : 0;
        _dragEnabled.IsChecked = options.DragEnabled;
        _resetPosition.IsEnabled = options.HasOffset;
        _positionError.Text = saved ? string.Empty : L.T("toast.status.saveFailed");
        _positionError.Visibility = saved ? Visibility.Collapsed : Visibility.Visible;
        _ready = ready;
    }

    private void OnCornerChanged()
    {
        if (!_ready) return;
        ApplyPosition(NwdActivityToast.PositionOptions.WithCorner(_horizontal.SelectedIndex == 1, _vertical.SelectedIndex == 1));
    }

    private void OnDragChanged()
    {
        if (!_ready) return;
        ApplyPosition(NwdActivityToast.PositionOptions.WithDrag(_dragEnabled.IsChecked == true));
    }

    private void ApplyPosition(ToastPositionOptions options)
    {
        SyncPosition(NwdActivityToast.ApplyPosition(options));
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
        SyncPaused();
    }

    private void OnPauseToggled()
    {
        if (!_ready)
            return;
        var saved = NwdActivityToast.SetPaused(_paused.IsChecked == true);
        _pausedError.Visibility = saved ? Visibility.Collapsed : Visibility.Visible;
        SyncPaused();
    }

    /// <summary>Pause for today follows the saved state and is available only while toasts are on.</summary>
    internal void SyncPaused()
    {
        var ready = _ready;
        _ready = false;
        try
        {
            var paused = NwdActivityToast.Paused;
            if (_paused.IsChecked != paused)
                _paused.IsChecked = paused;
            _paused.IsEnabled = _toastEnabled.IsChecked == true;
        }
        finally
        {
            _ready = ready;
        }
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
        {
            _idleError.Text = L.T("settings.toast.idle.saveFailed");
            _idleError.Visibility = Visibility.Visible;
            return;
        }
        _idleError.Text = string.Empty;
        _idleError.Visibility = Visibility.Collapsed;
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
