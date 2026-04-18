using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GearGauge.UI.Controls;

public partial class ColorPickerControl : UserControl
{
    private static readonly string[] PresetColors =
    [
        // Neon / Cyber
        "#00FFCC", "#00D2FF", "#39FF14", "#FF6B6B",
        "#FFD93D", "#6BCB77", "#C084FC", "#FF69B4",
        // Ice / Cool
        "#7DF9FF", "#87CEEB", "#B0E0E6", "#6495ED",
        "#00BFFF", "#00CED1", "#AFEEEE", "#E6E6FA",
        // Warm / Fire
        "#FFBF00", "#FF6600", "#FF4444", "#FFD700",
        "#FFA500", "#FF4500", "#FF8C00", "#FF6347",
        // Green / Circuit
        "#00FF41", "#33CC33", "#50C878", "#3CB371",
        "#008080", "#20B2AA", "#66CDAA", "#00FF7F",
        // Violet / Purple
        "#BF40BF", "#DA70D6", "#FF00FF", "#9370DB",
        "#BA55D3", "#8A2BE2", "#DDA0DD", "#9932CC",
        // Red / Phantom
        "#DC143C", "#FF2400", "#B22222", "#FF0000",
        "#FF4500", "#CD5C5C", "#F08080", "#FA8072",
        // Gray / Titanium
        "#C0C0C0", "#D3D3D3", "#F5F5F5", "#A9A9A9",
        "#DCDCDC", "#E8E8E8", "#B0B0B0", "#C8C8C8",
        // Extra accents
        "#4ADE80", "#34D399", "#A78BFA", "#38BDF8",
        "#FB923C", "#F472B6", "#FBBF24", "#FFFFFF"
    ];

    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(nameof(Color), typeof(Color), typeof(ColorPickerControl),
            new FrameworkPropertyMetadata(Colors.Cyan, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    public static readonly DependencyProperty HexColorProperty =
        DependencyProperty.Register(nameof(HexColor), typeof(string), typeof(ColorPickerControl),
            new FrameworkPropertyMetadata("#00FFCC", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHexColorChanged));

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public string HexColor
    {
        get => (string)GetValue(HexColorProperty);
        set => SetValue(HexColorProperty, value);
    }

    private bool _isPopupActive;

    public ColorPickerControl()
    {
        InitializeComponent();
        BuildSwatches();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.Deactivated += OnParentWindowDeactivated;
            window.LocationChanged += OnParentWindowLocationChanged;
        }
    }

    private void OnParentWindowDeactivated(object? sender, EventArgs e)
    {
        ClosePopup();
    }

    private void OnParentWindowLocationChanged(object? sender, EventArgs e)
    {
        if (SwatchPopup.IsOpen)
        {
            var offset = SwatchPopup.HorizontalOffset;
            SwatchPopup.HorizontalOffset = offset + 1;
            SwatchPopup.HorizontalOffset = offset;
        }
    }

    private void BuildSwatches()
    {
        foreach (var hex in PresetColors)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var btn = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = brush,
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Tag = hex,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0))
            };

            btn.MouseDown += OnSwatchSelected;
            SwatchGrid.Children.Add(btn);
        }
    }

    private void OnSwatchMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (SwatchPopup.IsOpen)
        {
            ClosePopup();
        }
        else
        {
            SwatchPopup.IsOpen = true;
            StartListeningForOutsideClicks();
        }
    }

    private void OnSwatchSelected(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement fe && fe.Tag is string hex)
        {
            HexColor = hex;
            // 强制将值推送到外部绑定源（UpdateSourceTrigger=LostFocus 时不会自动推送）
            GetBindingExpression(HexColorProperty)?.UpdateSource();
            ClosePopup();
        }
    }

    private void StartListeningForOutsideClicks()
    {
        if (_isPopupActive) return;
        _isPopupActive = true;

        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnGlobalMouseDown), true);
        }
    }

    private void StopListeningForOutsideClicks()
    {
        if (!_isPopupActive) return;
        _isPopupActive = false;

        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnGlobalMouseDown));
        }
    }

    private void OnGlobalMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 如果点击目标在本控件或弹窗的视觉树内，不关闭
        if (IsDescendantOfControl(e.OriginalSource as DependencyObject))
            return;

        ClosePopup();
    }

    private bool IsDescendantOfControl(DependencyObject? obj)
    {
        // 检查是否在本控件的视觉树内
        var current = obj;
        while (current != null)
        {
            if (ReferenceEquals(current, this))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }

        // 弹窗内容在独立的视觉树中：检查是否属于 Popup 的根元素
        // Popup.RootVisual → Border → StackPanel → UniformGrid → swatch Border
        if (SwatchPopup.Child is FrameworkElement popupRoot)
        {
            current = obj;
            while (current != null)
            {
                if (ReferenceEquals(current, popupRoot))
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
        }

        return false;
    }

    private void ClosePopup()
    {
        SwatchPopup.IsOpen = false;
        StopListeningForOutsideClicks();
    }

    private void OnHexLostFocus(object sender, RoutedEventArgs e)
    {
        SyncHexToColor();
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ColorPickerControl)d;
        var c = (Color)e.NewValue;
        var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        if (!string.Equals(ctrl.HexColor, hex, StringComparison.OrdinalIgnoreCase))
        {
            ctrl.SetCurrentValue(HexColorProperty, hex);
        }
    }

    private static void OnHexColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ColorPickerControl)d;
        ctrl.SyncHexToColor();
    }

    private void SyncHexToColor()
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(HexColor);
            if (c != Color)
            {
                SetCurrentValue(ColorProperty, c);
            }
        }
        catch
        {
            // invalid hex — ignore
        }
    }
}
