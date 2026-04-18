using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GearGauge.UI.Controls;

public partial class SpinEditControl : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SpinEditControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SpinEditControl),
            new FrameworkPropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SpinEditControl),
            new FrameworkPropertyMetadata(100.0));

    public static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register(nameof(Increment), typeof(double), typeof(SpinEditControl),
            new FrameworkPropertyMetadata(1.0));

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(SpinEditControl),
            new FrameworkPropertyMetadata(0));

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(SpinEditControl),
            new FrameworkPropertyMetadata("0"));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Increment
    {
        get => (double)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public SpinEditControl()
    {
        InitializeComponent();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SpinEditControl)d;
        var val = (double)e.NewValue;
        ctrl.ValueText = ctrl.DecimalPlaces > 0
            ? val.ToString($"F{ctrl.DecimalPlaces}")
            : ((int)Math.Round(val)).ToString();
    }

    private void OnUpClick(object sender, RoutedEventArgs e)
    {
        var newVal = Value + Increment;
        if (newVal > Maximum) newVal = Maximum;
        Value = newVal;
    }

    private void OnDownClick(object sender, RoutedEventArgs e)
    {
        var newVal = Value - Increment;
        if (newVal < Minimum) newVal = Minimum;
        Value = newVal;
    }

    private void OnInputGotFocus(object sender, RoutedEventArgs e)
    {
        MainBorder.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void OnInputLostFocus(object sender, RoutedEventArgs e)
    {
        MainBorder.BorderBrush = (Brush)FindResource("InputBorderBrush");
        SyncTextToValue();
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var c in e.Text)
        {
            if (!char.IsDigit(c) && c != '-' && c != '.' && c != ',')
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void SyncTextToValue()
    {
        var text = ValueInput.Text?.Trim() ?? string.Empty;
        if (double.TryParse(text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var parsed) ||
            double.TryParse(text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out parsed))
        {
            var clamped = Math.Max(Minimum, Math.Min(Maximum, parsed));
            if (DecimalPlaces == 0)
                clamped = Math.Round(clamped);
            else
                clamped = Math.Round(clamped, DecimalPlaces);

            Value = clamped;
        }
        else
        {
            ValueText = DecimalPlaces > 0
                ? Value.ToString($"F{DecimalPlaces}")
                : ((int)Math.Round(Value)).ToString();
        }
    }
}
