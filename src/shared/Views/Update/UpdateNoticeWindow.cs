#nullable disable
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Bimwright.Nwd.Shared.Update;
using Bimwright.Nwd.Shared.Views.Toast;

namespace Bimwright.Nwd.Shared.Views.Update
{
    /// <summary>
    /// Small modeless "update available" card in the bottom-right corner of the work area
    /// (family parity with rvt-mcp / ipt-mcp / dwg-mcp). Never modal, never takes focus.
    /// </summary>
    internal sealed class UpdateNoticeWindow : Window
    {
        private readonly UpdateInfo _info;
        private readonly string _statePath;
        private readonly string _prompt;
        private readonly Button _copy;

        public UpdateNoticeWindow(UpdateInfo info, string statePath)
        {
            _info = info ?? throw new ArgumentNullException(nameof(info));
            _statePath = statePath;
            _prompt = UpdateNoticeText.BuildAgentPrompt(info);

            Title = BrandAssets.ProductName + " update available";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            FontFamily = McpToastTheme.UiFont;
            FontSize = 13;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            var scrollResources = ToastScrollBarStyles.Create();
            Resources.MergedDictionaries.Add(scrollResources);
            var thinScrollBarStyle = scrollResources["ToastThinScrollBar"] as Style;
            if (thinScrollBarStyle != null)
                Resources[typeof(ScrollBar)] = thinScrollBarStyle;

            var bg = McpToastTheme.Background;
            var fg = McpToastTheme.Text;
            var secondary = McpToastTheme.TextSecondary;
            var surface = FrozenBrush(0xFF, 0xFF, 0xFF);
            var border = McpToastTheme.ActivityBorder;
            var primary = McpToastTheme.Primary;
            var primaryHover = FrozenBrush(0x00, 0x62, 0xA3);
            var pillBg = FrozenBrush(0xF1, 0xF5, 0xF9);
            var secondaryHoverBg = FrozenBrush(0xEA, 0xEE, 0xF4);

            Background = bg;
            Foreground = fg;

            var root = new StackPanel { Margin = new Thickness(16, 14, 16, 12) };

            // Header: version pill on the left, subtle release-page link on the right.
            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pillText = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            pillText.Children.Add(new TextBlock { Text = info.CurrentVersion, Foreground = secondary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            pillText.Children.Add(new TextBlock { Text = " → ", Foreground = secondary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            pillText.Children.Add(new TextBlock { Text = info.LatestVersion, Foreground = primary, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var versionPill = new Border
            {
                Background = pillBg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 3, 9, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = pillText,
            };
            Grid.SetColumn(versionPill, 0);
            header.Children.Add(versionPill);

            var openLink = CreateLinkButton("Open release page ↗", primary, FontWeights.SemiBold);
            openLink.Click += (_, __) => OpenUrl(_info.ReleaseUrl);
            Grid.SetColumn(openLink, 1);
            header.Children.Add(openLink);
            root.Children.Add(header);

            root.Children.Add(new TextBlock
            {
                Text = "A new version of " + BrandAssets.ProductName + " is available (" + info.LatestVersion + "). Copy the prompt below and paste it to your AI agent to update.",
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10),
            });

            if (!string.IsNullOrWhiteSpace(info.ReleaseNotes))
            {
                root.Children.Add(new TextBlock
                {
                    Text = "What's new",
                    Foreground = secondary,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4),
                });
                var notesBox = new TextBox
                {
                    Text = info.ReleaseNotes,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 160,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Foreground = fg,
                    FontSize = 12,
                    Padding = new Thickness(10, 8, 10, 8),
                };
                AttachSmoothScrollBarFade(notesBox);
                root.Children.Add(new Border
                {
                    Background = surface,
                    BorderBrush = border,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = notesBox,
                });
            }

            var promptExpanded = string.IsNullOrWhiteSpace(info.ReleaseNotes);
            var promptTextBox = new TextBox
            {
                Text = _prompt,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 130,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = fg,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(8, 6, 8, 6),
            };
            AttachSmoothScrollBarFade(promptTextBox);
            var promptBox = new Border
            {
                Background = surface,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 4, 0, 12),
                Visibility = promptExpanded ? Visibility.Visible : Visibility.Collapsed,
                Child = promptTextBox,
            };
            var promptToggle = CreateLinkButton((promptExpanded ? "▾ " : "▸ ") + "Preview agent update prompt", secondary, FontWeights.Normal);
            promptToggle.HorizontalAlignment = HorizontalAlignment.Left;
            promptToggle.Margin = new Thickness(0, 0, 0, promptExpanded ? 0 : 8);
            promptToggle.Click += (_, __) =>
            {
                promptExpanded = !promptExpanded;
                promptToggle.Content = (promptExpanded ? "▾ " : "▸ ") + "Preview agent update prompt";
                promptToggle.Margin = new Thickness(0, 0, 0, promptExpanded ? 0 : 8);
                promptBox.Visibility = promptExpanded ? Visibility.Visible : Visibility.Collapsed;
            };
            root.Children.Add(promptToggle);
            root.Children.Add(promptBox);

            // Footer: single guaranteed row — Skip link on left, primary Copy + Close on right.
            var footerDivider = new Border
            {
                BorderBrush = border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 10, 0, 0),
            };
            var footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var skip = CreateLinkButton("Skip this version", secondary, FontWeights.Normal);
            skip.HorizontalAlignment = HorizontalAlignment.Left;
            skip.Click += (_, __) => { UpdateChecker.SkipVersion(_info.LatestVersion, _statePath); Close(); };
            Grid.SetColumn(skip, 0);
            footer.Children.Add(skip);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            _copy = CreateActionButton("Copy prompt", primary, primaryHover, Brushes.White, primary, minWidth: 104);
            _copy.Margin = new Thickness(0, 0, 8, 0);
            _copy.Click += (_, __) => CopyPrompt();
            var close = CreateActionButton("Close", surface, secondaryHoverBg, fg, border, minWidth: 72);
            close.IsCancel = true;
            close.Click += (_, __) => Close();
            actions.Children.Add(_copy);
            actions.Children.Add(close);
            Grid.SetColumn(actions, 1);
            footer.Children.Add(actions);

            footerDivider.Child = footer;
            root.Children.Add(footerDivider);
            Content = root;

            Loaded += (_, __) => PlaceBottomRight();
            SizeChanged += (_, __) => ClampInsideWorkArea();
        }

        private static SolidColorBrush FrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static Button CreateActionButton(string text, Brush background, Brush hoverBackground, Brush foreground, Brush borderBrush, double minWidth)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "BtnBorder";
            borderFactory.SetValue(Border.BackgroundProperty, background);
            borderFactory.SetValue(Border.BorderBrushProperty, borderBrush);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(presenter);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBackground, "BtnBorder"));
            template.Triggers.Add(hoverTrigger);

            return new Button
            {
                Content = text,
                Foreground = foreground,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                MinWidth = minWidth,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Template = template,
            };
        }

        private static Button CreateLinkButton(string text, Brush foreground, FontWeight weight)
        {
            var template = new ControlTemplate(typeof(Button));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            template.VisualTree = presenter;

            return new Button
            {
                Content = text,
                Foreground = foreground,
                FontSize = 12,
                FontWeight = weight,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 4, 0, 4),
                MinWidth = 0,
                Height = double.NaN,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Template = template,
            };
        }

        private static void AttachSmoothScrollBarFade(TextBox textBox)
        {
            var idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };

            ScrollBar FindVerticalScrollBar()
            {
                textBox.ApplyTemplate();
                var sv = textBox.Template?.FindName("PART_ContentHost", textBox) as ScrollViewer;
                sv?.ApplyTemplate();
                return sv?.Template?.FindName("PART_VerticalScrollBar", sv) as ScrollBar;
            }

            void AnimateOpacity(double to, int durationMs)
            {
                var sb = FindVerticalScrollBar();
                if (sb == null) return;
                sb.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }

            void Wake()
            {
                idleTimer.Stop();
                AnimateOpacity(0.92, 140);
                idleTimer.Start();
            }

            idleTimer.Tick += (_, __) =>
            {
                idleTimer.Stop();
                if (!textBox.IsMouseOver)
                    AnimateOpacity(0.22, 320);
            };

            textBox.MouseEnter += (_, __) => { idleTimer.Stop(); AnimateOpacity(0.92, 140); };
            textBox.MouseLeave += (_, __) => { idleTimer.Stop(); AnimateOpacity(0.22, 320); };
            textBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, __) => Wake()));
        }

        private void CopyPrompt()
        {
            try
            {
                Clipboard.SetText(_prompt);
                _copy.Content = "Copied ✓";
            }
            catch { }
        }

        private void PlaceBottomRight()
        {
            try
            {
                var area = SystemParameters.WorkArea;
                Left = area.Right - ActualWidth - 16;
                Top = area.Bottom - ActualHeight - 16;
            }
            catch { }
        }

        private void ClampInsideWorkArea()
        {
            if (!IsLoaded) return;
            try
            {
                var area = SystemParameters.WorkArea;
                if (Top + ActualHeight > area.Bottom - 16)
                    Top = Math.Max(area.Top + 16, area.Bottom - ActualHeight - 16);
            }
            catch { }
        }

        private static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }
}
