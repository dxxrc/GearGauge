using System.Windows.Media;

namespace GearGauge.UI.ViewModels;

public sealed class ThemeViewModel : ObservableViewModel
{
    private Brush _windowBackgroundBrush = Brushes.White;
    private Brush _navigationBackgroundBrush = Brushes.White;
    private Brush _pageBackgroundBrush = Brushes.White;
    private Brush _cardBackgroundBrush = Brushes.White;
    private Brush _secondaryCardBackgroundBrush = Brushes.White;
    private Brush _borderBrush = Brushes.LightGray;
    private Brush _accentBrush = Brushes.DodgerBlue;
    private Brush _accentForegroundBrush = Brushes.White;
    private Brush _textPrimaryBrush = Brushes.Black;
    private Brush _textSecondaryBrush = Brushes.Gray;
    private Brush _textMutedBrush = Brushes.DimGray;
    private Brush _inputBackgroundBrush = Brushes.WhiteSmoke;
    private Brush _inputBorderBrush = Brushes.LightGray;
    private Brush _selectedNavigationBackgroundBrush = Brushes.LightBlue;
    private Brush _hoverNavigationBackgroundBrush = Brushes.Gainsboro;
    private Brush _separatorBrush = Brushes.Gainsboro;
    private Brush _windowChromeBrush = Brushes.White;

    public Brush WindowBackgroundBrush { get => _windowBackgroundBrush; set => SetProperty(ref _windowBackgroundBrush, value); }
    public Brush NavigationBackgroundBrush { get => _navigationBackgroundBrush; set => SetProperty(ref _navigationBackgroundBrush, value); }
    public Brush PageBackgroundBrush { get => _pageBackgroundBrush; set => SetProperty(ref _pageBackgroundBrush, value); }
    public Brush CardBackgroundBrush { get => _cardBackgroundBrush; set => SetProperty(ref _cardBackgroundBrush, value); }
    public Brush SecondaryCardBackgroundBrush { get => _secondaryCardBackgroundBrush; set => SetProperty(ref _secondaryCardBackgroundBrush, value); }
    public Brush BorderBrush { get => _borderBrush; set => SetProperty(ref _borderBrush, value); }
    public Brush AccentBrush { get => _accentBrush; set => SetProperty(ref _accentBrush, value); }
    public Brush AccentForegroundBrush { get => _accentForegroundBrush; set => SetProperty(ref _accentForegroundBrush, value); }
    public Brush TextPrimaryBrush { get => _textPrimaryBrush; set => SetProperty(ref _textPrimaryBrush, value); }
    public Brush TextSecondaryBrush { get => _textSecondaryBrush; set => SetProperty(ref _textSecondaryBrush, value); }
    public Brush TextMutedBrush { get => _textMutedBrush; set => SetProperty(ref _textMutedBrush, value); }
    public Brush InputBackgroundBrush { get => _inputBackgroundBrush; set => SetProperty(ref _inputBackgroundBrush, value); }
    public Brush InputBorderBrush { get => _inputBorderBrush; set => SetProperty(ref _inputBorderBrush, value); }
    public Brush SelectedNavigationBackgroundBrush { get => _selectedNavigationBackgroundBrush; set => SetProperty(ref _selectedNavigationBackgroundBrush, value); }
    public Brush HoverNavigationBackgroundBrush { get => _hoverNavigationBackgroundBrush; set => SetProperty(ref _hoverNavigationBackgroundBrush, value); }
    public Brush SeparatorBrush { get => _separatorBrush; set => SetProperty(ref _separatorBrush, value); }
    public Brush WindowChromeBrush { get => _windowChromeBrush; set => SetProperty(ref _windowChromeBrush, value); }
}
