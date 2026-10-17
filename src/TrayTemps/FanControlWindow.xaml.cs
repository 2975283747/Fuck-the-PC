using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfButton = System.Windows.Controls.Button;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfCursors = System.Windows.Input.Cursors;

namespace TrayTemps;

internal partial class FanControlWindow : Window
{
    private readonly FanControlService _fanControl;
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<FanCardViewModel> _fans = [];
    private readonly ObservableCollection<ProfileCardViewModel> _profiles = [];
    private readonly ObservableCollection<ProfileChoiceViewModel> _profileChoices = [];

    private string _language = "zh-CN";
    private string _themeMode = "System";
    private string _resolvedTheme = string.Empty;
    private bool _showHidden;
    private bool _allowClose;
    private bool _refreshing;
    private bool _isMaximized;
    private bool _isAnimatingWindow;
    private Rect _restoreBounds;

    internal FanControlWindow(FanControlService fanControl)
    {
        _fanControl = fanControl;
        InitializeComponent();
        _restoreBounds = new Rect(Left, Top, Width, Height);
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
            {
                _isMaximized = true;
                UpdateMaxGlyph();
            }
        };

        DataContext = this;
        FansView = CollectionViewSource.GetDefaultView(_fans);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => RefreshFromService();
        _timer.Start();

        ApplyLanguage(_fanControl.Language);
        SetSection(fansSection: true);
        RefreshFromService();
    }

    public ICollectionView FansView { get; }

    public ObservableCollection<ProfileChoiceViewModel> ProfileChoices => _profileChoices;

    public ObservableCollection<ProfileCardViewModel> Profiles => _profiles;

    internal void AllowClose()
    {
        _allowClose = true;
        _timer.Stop();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _timer.Stop();
        base.OnClosing(e);
    }

    private void RefreshFromService()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            FanControlUiState state = _fanControl.GetUiState();

            if (!string.Equals(_themeMode, state.ThemeMode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    _resolvedTheme,
                    ThemeManager.Resolve(state.ThemeMode),
                    StringComparison.OrdinalIgnoreCase))
            {
                _themeMode = state.ThemeMode;
                ApplyTheme();
            }

            if (!string.Equals(_language, state.Language, StringComparison.OrdinalIgnoreCase))
            {
                ApplyLanguage(state.Language);
            }

            UpdateProfileChoices(state.ProfileChoices);
            UpdateFans(state.Fans);
            UpdateProfiles(state.Profiles);

            string status =
                $"{Strings.Get(_language, "Detected")} {state.DetectedFanCount} " +
                $"{Strings.Get(_language, "FansDetected")}, " +
                $"{state.DetectedTemperatureCount} " +
                $"{Strings.Get(_language, "TempsDetected")}";

            WindowStatusText.Text = status;
            StatusText.Text = state.FirstScanCompleted
                ? status
                : Strings.Get(_language, "FirstScan");
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateProfileChoices(IReadOnlyList<ProfileChoice> choices)
    {
        var desired = choices.ToDictionary(
            static choice => choice.Id,
            static choice => choice.Name,
            StringComparer.Ordinal);

        for (int index = _profileChoices.Count - 1; index >= 0; index--)
        {
            if (!desired.ContainsKey(_profileChoices[index].Id))
            {
                _profileChoices.RemoveAt(index);
            }
        }

        foreach (ProfileChoice choice in choices)
        {
            ProfileChoiceViewModel? existing = _profileChoices.FirstOrDefault(
                item => item.Id == choice.Id);

            if (existing is null)
            {
                _profileChoices.Add(new ProfileChoiceViewModel(choice.Id, choice.Name));
            }
            else
            {
                existing.Name = choice.Name;
            }
        }
    }

    private void UpdateFans(IReadOnlyList<FanCardState> fans)
    {
        var desiredIds = fans.Select(static fan => fan.ControlId)
            .ToHashSet(StringComparer.Ordinal);

        for (int index = _fans.Count - 1; index >= 0; index--)
        {
            if (!desiredIds.Contains(_fans[index].ControlId))
            {
                _fans.RemoveAt(index);
            }
        }

        foreach (FanCardState state in fans)
        {
            FanCardViewModel? viewModel = _fans.FirstOrDefault(
                item => item.ControlId == state.ControlId);

            if (viewModel is null)
            {
                viewModel = new FanCardViewModel(state.ControlId);
                _fans.Add(viewModel);
            }

            viewModel.Name = state.Name;
            viewModel.HardwareName = state.HardwareName;
            viewModel.FanSensorName = state.FanSensorName;
            viewModel.IsEnabled = state.IsEnabled;
            viewModel.IsHidden = state.IsHidden;
            viewModel.IsVisible = _showHidden || !state.IsHidden;
            viewModel.RpmText = state.Rpm is float rpm ? $"{rpm:0} RPM" : "-- RPM";
            viewModel.PercentText = state.Percent is float percent ? $"{percent:0} %" : "-- %";
            viewModel.PercentValue = state.Percent is float value ? value : 0;
            viewModel.SelectedProfileId = state.ProfileId;
            viewModel.Error = state.Error ?? string.Empty;
            viewModel.HideMenuText = Strings.Get(
                _language,
                state.IsHidden ? "Unhide" : "Hide");
            viewModel.StatusText = state.IsEnabled
                ? Strings.Get(_language, "On")
                : Strings.Get(_language, "Off");

            List<ProfileChoiceViewModel> choices = _profileChoices.ToList();
            if (!viewModel.ProfileChoices.SequenceEqual(choices))
            {
                viewModel.ProfileChoices = choices;
            }
        }
    }

    private void UpdateProfiles(IReadOnlyList<ProfileCardState> profiles)
    {
        var desiredIds = profiles.Select(static profile => profile.Id)
            .ToHashSet(StringComparer.Ordinal);

        for (int index = _profiles.Count - 1; index >= 0; index--)
        {
            if (!desiredIds.Contains(_profiles[index].Id))
            {
                _profiles.RemoveAt(index);
            }
        }

        foreach (ProfileCardState state in profiles)
        {
            ProfileCardViewModel? viewModel = _profiles.FirstOrDefault(
                item => item.Id == state.Id);

            if (viewModel is null)
            {
                viewModel = new ProfileCardViewModel(state.Id);
                _profiles.Add(viewModel);
            }

            viewModel.Name = state.Name;
            viewModel.ModeText = Strings.Get(_language, state.Mode switch
            {
                SpeedMode.Fixed => "Fixed",
                SpeedMode.Linear => "Linear",
                _ => "Curve"
            });
            viewModel.SensorName = string.IsNullOrWhiteSpace(state.TemperatureSensorName)
                ? Strings.Get(_language, "NoTemperature")
                : state.TemperatureSensorName;
            viewModel.Summary = state.Mode == SpeedMode.Curve
                ? $"{state.CurvePoints.Count} {Strings.Get(_language, "Points")}"
                : state.Summary;
            viewModel.EditText = Strings.Get(_language, "Edit");
            viewModel.DeleteText = Strings.Get(_language, "Delete");
        }
    }

    private void ApplyLanguage(string language)
    {
        _language = language.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "zh-CN";

        FansNavButton.Content = Strings.Get(_language, "Fans");
        ProfilesNavButton.Content = Strings.Get(_language, "Profiles");
        AboutNavButton.Content = Strings.Get(_language, "About");
        UpdateThemeButtonToolTip();
        FansTitleText.Text = Strings.Get(_language, "FansTitle");
        FansSubtitleText.Text = Strings.Get(_language, "FansSubtitle");
        ProfilesTitleText.Text = Strings.Get(_language, "ProfilesTitle");
        ProfilesSubtitleText.Text = Strings.Get(_language, "ProfilesSubtitle");
        AddFixedButton.Content = Strings.Get(_language, "AddFixed");
        AddLinearButton.Content = Strings.Get(_language, "AddLinear");
        AddCurveButton.Content = Strings.Get(_language, "AddCurve");
        ShowHiddenCheckBox.Content = Strings.Get(_language, "ShowHidden");

        ZhButton.Background = _language == "zh-CN"
            ? new SolidColorBrush(WpfColor.FromRgb(0, 113, 227))
            : new SolidColorBrush(WpfColor.FromRgb(236, 236, 240));
        ZhButton.Foreground = _language == "zh-CN"
            ? WpfBrushes.White
            : new SolidColorBrush(WpfColor.FromRgb(29, 29, 31));
        EnButton.Background = _language == "en-US"
            ? new SolidColorBrush(WpfColor.FromRgb(0, 113, 227))
            : new SolidColorBrush(WpfColor.FromRgb(236, 236, 240));
        EnButton.Foreground = _language == "en-US"
            ? WpfBrushes.White
            : new SolidColorBrush(WpfColor.FromRgb(29, 29, 31));
    }

    private void SetSection(bool fansSection)
    {
        FansHeader.Visibility = fansSection ? Visibility.Visible : Visibility.Collapsed;
        FansScrollViewer.Visibility = fansSection ? Visibility.Visible : Visibility.Collapsed;
        ProfilesHeader.Visibility = fansSection ? Visibility.Collapsed : Visibility.Visible;
        ProfilesScrollViewer.Visibility = fansSection ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private const int WmNcLButtonDown = 0x00A1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int message,
        IntPtr wParam,
        IntPtr lParam);

    private void Window_PreviewMouseMove(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        if (_isMaximized || _isAnimatingWindow)
        {
            Cursor = WpfCursors.Arrow;
            return;
        }

        System.Windows.Point position = e.GetPosition(this);
        int hitTest = GetResizeHitTest(position);
        Cursor = hitTest switch
        {
            HtLeft or HtRight => WpfCursors.SizeWE,
            HtTop or HtBottom => WpfCursors.SizeNS,
            HtTopLeft or HtBottomRight => WpfCursors.SizeNWSE,
            HtTopRight or HtBottomLeft => WpfCursors.SizeNESW,
            _ => WpfCursors.Arrow
        };
    }

    private void Window_PreviewMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isMaximized || _isAnimatingWindow)
        {
            return;
        }

        int hitTest = GetResizeHitTest(e.GetPosition(this));
        if (hitTest == 0)
        {
            return;
        }

        e.Handled = true;
        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SendMessage(handle, WmNcLButtonDown, (IntPtr)hitTest, IntPtr.Zero);
    }

    private int GetResizeHitTest(System.Windows.Point position)
    {
        const double border = 6;
        bool left = position.X <= border;
        bool right = position.X >= ActualWidth - border;
        bool top = position.Y <= border;
        bool bottom = position.Y >= ActualHeight - border;

        if (top && left) return HtTopLeft;
        if (top && right) return HtTopRight;
        if (bottom && left) return HtBottomLeft;
        if (bottom && right) return HtBottomRight;
        if (left) return HtLeft;
        if (right) return HtRight;
        if (top) return HtTop;
        if (bottom) return HtBottom;
        return 0;
    }

    private void ToggleMaximize()
    {
        if (_isAnimatingWindow)
        {
            return;
        }

        if (_isMaximized)
        {
            AnimateWindowTo(_restoreBounds, maximized: false);
            return;
        }

        _restoreBounds = new Rect(Left, Top, Width, Height);
        AnimateWindowTo(GetMaximizedBounds(), maximized: true);
    }

    private void AnimateWindowTo(Rect target, bool maximized)
    {
        _isAnimatingWindow = true;
        WindowState = WindowState.Normal;

        var duration = new Duration(TimeSpan.FromMilliseconds(240));
        var easing = new CubicEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        var leftAnimation = new DoubleAnimation(Left, target.Left, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        var topAnimation = new DoubleAnimation(Top, target.Top, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        var widthAnimation = new DoubleAnimation(Width, target.Width, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        var heightAnimation = new DoubleAnimation(Height, target.Height, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };

        leftAnimation.Completed += (_, _) => CompleteWindowAnimation(target, maximized);
        BeginAnimation(Window.LeftProperty, leftAnimation, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(Window.TopProperty, topAnimation, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(Window.WidthProperty, widthAnimation, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(Window.HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void CompleteWindowAnimation(Rect target, bool maximized)
    {
        BeginAnimation(Window.LeftProperty, null);
        BeginAnimation(Window.TopProperty, null);
        BeginAnimation(Window.WidthProperty, null);
        BeginAnimation(Window.HeightProperty, null);
        Left = target.Left;
        Top = target.Top;
        Width = target.Width;
        Height = target.Height;
        _isMaximized = maximized;
        _isAnimatingWindow = false;
        UpdateMaxGlyph();
    }

    private Rect GetMaximizedBounds()
    {
        System.Drawing.Rectangle workingArea = System.Windows.Forms.Screen
            .FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle)
            .WorkingArea;
        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        return new Rect(
            workingArea.Left / dpi.DpiScaleX,
            workingArea.Top / dpi.DpiScaleY,
            workingArea.Width / dpi.DpiScaleX,
            workingArea.Height / dpi.DpiScaleY);
    }

    private void UpdateMaxGlyph()
    {
        bool maximized = _isMaximized;
        MaxGlyph.Visibility = Visibility.Visible;
        RestoreGlyph.Visibility = maximized
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MinButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaxButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void FansNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetSection(fansSection: true);
    }

    private void ProfilesNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetSection(fansSection: false);
    }

    private void AboutNavButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new FanAboutWindow(_language, _themeMode)
        {
            Owner = this
        };
        about.ShowDialog();
    }

    private void FanToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle ||
            toggle.DataContext is not FanCardViewModel fan)
        {
            return;
        }

        _fanControl.SetFanEnabled(fan.ControlId, toggle.IsChecked == true);
        RefreshFromService();
    }

    private void FanProfile_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_refreshing ||
            sender is not WpfComboBox combo ||
            combo.DataContext is not FanCardViewModel fan)
        {
            return;
        }

        string? profileId = combo.SelectedValue as string;
        _fanControl.SetFanProfile(fan.ControlId, profileId);
        RefreshFromService();
    }

    private void FanContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu ||
            menu.PlacementTarget is not FrameworkElement target ||
            target.DataContext is not FanCardViewModel fan ||
            menu.Items.Count < 2 ||
            menu.Items[0] is not MenuItem renameItem ||
            menu.Items[1] is not MenuItem hideItem)
        {
            return;
        }

        renameItem.Header = Strings.Get(_language, "Rename");
        hideItem.Header = fan.HideMenuText;
    }

    private void FanRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item ||
            item.Parent is not ContextMenu menu ||
            menu.PlacementTarget is not FrameworkElement target ||
            target.DataContext is not FanCardViewModel fan)
        {
            return;
        }

        var input = new TextInputWindow(
            Strings.Get(_language, "Rename"),
            Strings.Get(_language, "RenameFan"),
            fan.Name,
            Strings.Get(_language, "Ok"),
            Strings.Get(_language, "Cancel"),
            _themeMode)
        {
            Owner = this
        };

        if (input.ShowDialog() == true &&
            !string.IsNullOrWhiteSpace(input.Value))
        {
            _fanControl.SetFanName(fan.ControlId, input.Value);
            RefreshFromService();
        }
    }

    private void FanHide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item ||
            item.Parent is not ContextMenu menu ||
            menu.PlacementTarget is not FrameworkElement target ||
            target.DataContext is not FanCardViewModel fan)
        {
            return;
        }

        _fanControl.SetFanHidden(fan.ControlId, !fan.IsHidden);
        RefreshFromService();
    }

    private void ShowHiddenCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _showHidden = ShowHiddenCheckBox.IsChecked == true;
        RefreshFromService();
    }

    private void ZhButton_Click(object sender, RoutedEventArgs e)
    {
        _fanControl.SetLanguage("zh-CN");
        ApplyLanguage("zh-CN");
        RefreshFromService();
    }

    private void EnButton_Click(object sender, RoutedEventArgs e)
    {
        _fanControl.SetLanguage("en-US");
        ApplyLanguage("en-US");
        RefreshFromService();
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        AddThemeMenuItem(menu, "System", Strings.Get(_language, "ThemeSystem"));
        AddThemeMenuItem(menu, "Light", Strings.Get(_language, "ThemeLight"));
        AddThemeMenuItem(menu, "Dark", Strings.Get(_language, "ThemeDark"));
        menu.PlacementTarget = ThemeButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void AddThemeMenuItem(
        ContextMenu menu,
        string mode,
        string label)
    {
        bool selected = string.Equals(
            _themeMode,
            mode,
            StringComparison.OrdinalIgnoreCase);
        var item = new MenuItem
        {
            Header = CreateThemeMenuHeader(label, selected),
            IsCheckable = false,
            Padding = new Thickness(0),
            StaysOpenOnClick = false
        };
        item.Click += (_, _) =>
        {
            _themeMode = mode;
            _fanControl.SetThemeMode(mode);
            ApplyTheme(animate: true);
            RefreshFromService();
        };
        menu.Items.Add(item);
    }

    private object CreateThemeMenuHeader(string label, bool selected)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = selected
                ? WpfBrushes.White
                : (WpfBrush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center
        };

        return new Border
        {
            Background = selected
                ? (WpfBrush)FindResource("Accent")
                : WpfBrushes.Transparent,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 7, 24, 7),
            Child = text,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            MinWidth = 126
        };
    }

    private void ApplyTheme(bool animate = true)
    {
        bool firstApply = string.IsNullOrEmpty(_resolvedTheme);
        ThemeManager.Apply(this, _themeMode, animate && !firstApply);
        _resolvedTheme = ThemeManager.Resolve(_themeMode);
        UpdateThemeButtonToolTip();
    }

    private void UpdateThemeButtonToolTip()
    {
        ThemeButton.ToolTip = Strings.Get(_language, "Skin");
    }

    private void AddFixedButton_Click(object sender, RoutedEventArgs e)
    {
        AddProfile(SpeedMode.Fixed);
    }

    private void AddLinearButton_Click(object sender, RoutedEventArgs e)
    {
        AddProfile(SpeedMode.Linear);
    }

    private void AddCurveButton_Click(object sender, RoutedEventArgs e)
    {
        AddProfile(SpeedMode.Curve);
    }

    private void AddProfile(SpeedMode mode)
    {
        FanControlUiState state = _fanControl.GetUiState();
        TemperatureChoice? sensor = state.Temperatures.FirstOrDefault();

        if (sensor is null)
        {
            System.Windows.MessageBox.Show(
                Strings.Get(_language, "NoTemperature"),
                Strings.Get(_language, "AppTitle"));
            return;
        }

        string modeName = Strings.Get(_language, mode switch
        {
            SpeedMode.Fixed => "Fixed",
            SpeedMode.Linear => "Linear",
            _ => "Curve"
        });
        var profile = new SpeedProfileConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Mode = mode,
            TemperatureSensorId = sensor.Id,
            Name = $"{Strings.Get(_language, "ProfileCreated")} · {modeName}"
        };

        if (mode == SpeedMode.Curve)
        {
            profile.CurvePoints =
            [
                new CurvePoint { Temperature = 40, Percent = 25 },
                new CurvePoint { Temperature = 60, Percent = 50 },
                new CurvePoint { Temperature = 80, Percent = 100 }
            ];
        }

        OpenProfileEditor(profile, isNew: true);
    }

    private void EditProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button ||
            button.Tag is not ProfileCardViewModel card)
        {
            return;
        }

        SpeedProfileConfig? profile = _fanControl.GetProfile(card.Id);
        if (profile is not null)
        {
            OpenProfileEditor(profile, isNew: false);
        }
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button ||
            button.Tag is not ProfileCardViewModel card)
        {
            return;
        }

        MessageBoxResult result = System.Windows.MessageBox.Show(
            Strings.Get(_language, "DeleteProfile"),
            Strings.Get(_language, "DeleteProfileTitle"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        _fanControl.DeleteProfile(card.Id);
        RefreshFromService();
    }

    private void OpenProfileEditor(SpeedProfileConfig profile, bool isNew)
    {
        var editor = new SpeedProfileEditorWindow(
            profile,
            () => _fanControl.GetUiState().Temperatures,
            _language,
            _themeMode)
        {
            Owner = this
        };

        if (editor.ShowDialog() != true || editor.Profile is null)
        {
            return;
        }

        if (isNew)
        {
            _fanControl.AddProfile(editor.Profile);
        }
        else
        {
            _fanControl.UpdateProfile(editor.Profile);
        }

        RefreshFromService();
    }
}
