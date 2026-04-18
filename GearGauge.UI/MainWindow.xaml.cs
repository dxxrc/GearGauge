using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GearGauge.Core.Contracts;
using GearGauge.Core.Models;
using GearGauge.Hardware;
using GearGauge.UI.Localization;
using GearGauge.UI.Settings;
using GearGauge.UI.Taskbar;
using GearGauge.UI.Themes;
using GearGauge.UI.ViewModels;

namespace GearGauge.UI;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly CancellationTokenSource _refreshCancellation = new();
    private readonly UiSettingsStore _settingsStore = new();
    private readonly UiLocalizer _localizer = new();
    private UiSettings _settings;
    private IReadOnlyDictionary<string, string> _texts = new Dictionary<string, string>();
    private IMonitoringOrchestrator? _orchestrator;
    private Task? _refreshLoopTask;
    private HardwareMetrics? _lastSnapshot;
    private bool _isShutdownInProgress;
    private DispatcherTimer? _toastTimer;
    private OverlayWindow? _overlayWindow;
    private TaskbarWidgetWindow? _taskbarWidget;

    public MainWindow()
    {
        _settings = _settingsStore.Load();
        _viewModel.Settings.Load(_settings);
        _viewModel.IsMonitorPageSelected = true;

        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.Settings.Changed += OnSettingsChanged;

        ApplySettingsPresentation();

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _viewModel.StatusText = GetText("Initializing");

        try
        {
            _orchestrator = new MonitoringOrchestrator(
                new HardwareMonitorService(new LibreHardwareMonitorAdapter(), new MonitorInfoProvider()),
                new FpsMonitorService(),
                new NetworkMonitorService());

            await _orchestrator.InitializeAsync(_refreshCancellation.Token);
            await RefreshOnceAsync(_refreshCancellation.Token);
            _viewModel.StatusText = GetText("MonitoringActive");

            LoadOverlaySettings();
            ShowOverlayIfNeeded();

            LoadTaskbarSettings();
            ShowTaskbarWidgetIfNeeded();

            // 启动完成后同步语言文本到托盘菜单
            var app = (App)Application.Current;
            app.UpdateTraySettings(_settings, _texts);

            _refreshLoopTask = RunRefreshLoopAsync(_refreshCancellation.Token);
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"{GetText("InitializationFailed")}: {ex.Message}";
        }
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(_settings.SampleIntervalMs), cancellationToken);
            await RefreshOnceAsync(cancellationToken);
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        if (_orchestrator is null)
        {
            return;
        }

        try
        {
            var snapshot = await _orchestrator.CaptureAsync(cancellationToken);
            await Dispatcher.InvokeAsync(() => ApplySnapshot(snapshot), System.Windows.Threading.DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                _viewModel.StatusText = $"{GetText("RefreshFailed")}: {ex.Message}";
                _viewModel.LastUpdatedText = $"{GetText("LastAttempt")} {DateTime.Now:HH:mm:ss}";
            });
        }
    }

    private void ApplySnapshot(HardwareMetrics snapshot)
    {
        _lastSnapshot = snapshot;
        RefreshDeviceOptions(snapshot);

        _viewModel.StatusText = GetText("MonitoringActive");
        _viewModel.LastUpdatedText = $"{GetText("LastUpdatedPrefix")} {snapshot.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        _viewModel.CpuHeader = $"{GetText("CpuSection")} | {snapshot.Cpu.ModelName}";
        _viewModel.DataSourceBadge = $"{GetText("SourcePrefix")}: {snapshot.DataSourceInfo}";
        _viewModel.ElevationBadge = snapshot.IsElevated
            ? GetText("Elevated")
            : GetText("NotElevated");
        _viewModel.CanElevate = !snapshot.IsElevated;

        SyncMetricRows(_viewModel.CpuSummary,
            (GetText("Usage"), FormatCpuUsage(snapshot.Cpu.UsagePercent)),
            (GetText("Temperature"), UiValueFormatter.FormatTemperature(snapshot.Cpu.TemperatureCelsius, _settings, _settings.CpuTemperatureDecimals)),
            (GetText("Power"), UiValueFormatter.FormatPower(snapshot.Cpu.PowerWatt, _settings.CpuPowerDecimals)),
            (GetText("Clock"), UiValueFormatter.FormatCpuClock(snapshot.Cpu.ClockGHz, _settings)));

        SyncCollection(
            _viewModel.CpuCores,
            snapshot.Cpu.Cores,
            core => core.Index,
            viewModel => viewModel.Index,
            core => new CpuCoreViewModel { Index = core.Index },
            (viewModel, core) =>
            {
                viewModel.Title = $"{GetText("Core")} {core.Index}";
                viewModel.UsageText = $"{GetText("Usage")} {FormatCpuUsage(core.UsagePercent)}";
                viewModel.ClockText = $"{GetText("Clock")} {UiValueFormatter.FormatCpuClock(core.ClockGHz, _settings)}";
                viewModel.TemperatureText = $"{GetText("Temperature")} {UiValueFormatter.FormatTemperature(core.TemperatureCelsius, _settings, _settings.CpuTemperatureDecimals)}";
                viewModel.PowerText = $"{GetText("Power")} {UiValueFormatter.FormatPower(core.PowerWatt, _settings.CpuPowerDecimals)}";
            });

        var visibleGpus = FilterSelectedGpus(snapshot.Gpus).ToArray();
        SyncCollection(
            _viewModel.Gpus,
            visibleGpus,
            gpu => gpu.Id,
            viewModel => viewModel.Id,
            gpu => new GpuViewModel { Id = gpu.Id },
            (viewModel, gpu) =>
            {
                var activityState = gpu.IsActive ? GetText("ActiveState") : GetText("IdleState");
                viewModel.Header = $"{gpu.Name} [{gpu.Kind}] {activityState}";
                viewModel.UsageText = $"{GetText("Usage")} {FormatGpuUsage(gpu.UsagePercent)}";
                viewModel.TemperatureText = $"{GetText("Temperature")} {UiValueFormatter.FormatTemperature(gpu.TemperatureCelsius, _settings, _settings.GpuTemperatureDecimals)}";
                viewModel.PowerText = $"{GetText("Power")} {UiValueFormatter.FormatPower(gpu.PowerWatt, _settings.GpuPowerDecimals)}";
                viewModel.ClockText = $"{GetText("Clock")} {UiValueFormatter.FormatGpuClock(gpu.ClockMHz, _settings)}";
            });

        SyncMetricRows(_viewModel.MemoryRows,
            (GetText("Model"), Fallback(snapshot.Memory.ModelName)),
            (GetText("Capacity"), UiValueFormatter.FormatMemory(snapshot.Memory.TotalGB, _settings.MemoryCapacityDecimals)),
            (GetText("Used"), UiValueFormatter.FormatMemory(snapshot.Memory.UsedGB, _settings.MemoryCapacityDecimals)),
            (GetText("Available"), UiValueFormatter.FormatMemory(snapshot.Memory.AvailableGB, _settings.MemoryCapacityDecimals)),
            (GetText("Usage"), FormatMemoryUsage(snapshot.Memory.UsagePercent)));

        SyncMetricRows(_viewModel.FpsRows,
            (GetText("Status"), GetText("MonitoringActive")),
            (GetText("Display"), UiValueFormatter.FormatOptional(snapshot.Fps.DisplayOutputFps, "FPS", 2)),
            (GetText("Game"), UiValueFormatter.FormatOptional(snapshot.Fps.GameFps, "FPS", 2)),
            (GetText("Active"), Fallback(snapshot.Fps.ActiveContent)),
            (GetText("DisplayName"), Fallback(snapshot.Fps.TargetDisplayName)),
            (GetText("Provider"), Fallback(snapshot.Fps.ProviderStatus)));

        var visibleAdapters = FilterSelectedAdapters(snapshot.NetworkAdapters).ToArray();
        SyncCollection(
            _viewModel.NetworkAdapters,
            visibleAdapters,
            adapter => adapter.AdapterId,
            viewModel => viewModel.AdapterId,
            adapter => new NetworkAdapterViewModel { AdapterId = adapter.AdapterId },
            (viewModel, adapter) =>
            {
                var badge = adapter.IsSelected ? $" [{GetText("SelectedBadge")}]" : string.Empty;
                viewModel.Header = $"{adapter.AdapterName}{badge}";
                viewModel.StateText = $"{(adapter.IsUp ? GetText("Up") : GetText("Down"))} | {adapter.Type}";
                viewModel.DownloadText = $"{GetText("Download")} {UiValueFormatter.FormatNetworkSpeed(adapter.DownloadMbps, _settings)}";
                viewModel.UploadText = $"{GetText("Upload")} {UiValueFormatter.FormatNetworkSpeed(adapter.UploadMbps, _settings)}";
            });

        var focusedMetricSnapshot = CreateFocusedMetricSnapshot(snapshot);
        _overlayWindow?.ApplySnapshot(focusedMetricSnapshot, _settings, _texts);
        _taskbarWidget?.ApplySnapshot(focusedMetricSnapshot, _settings, _texts);
    }

    private IEnumerable<GpuMetrics> FilterSelectedGpus(IEnumerable<GpuMetrics> gpus)
    {
        return _settings.SelectedGpuId.Equals(UiSettings.AllDevicesValue, StringComparison.OrdinalIgnoreCase)
            ? gpus
            : gpus.Where(gpu => gpu.Id.Equals(_settings.SelectedGpuId, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<NetworkAdapterMetrics> FilterSelectedAdapters(IEnumerable<NetworkAdapterMetrics> adapters)
    {
        return _settings.SelectedNetworkAdapterId.Equals(UiSettings.AllDevicesValue, StringComparison.OrdinalIgnoreCase)
            ? adapters
            : adapters.Where(adapter => adapter.AdapterId.Equals(_settings.SelectedNetworkAdapterId, StringComparison.OrdinalIgnoreCase));
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var app = (App)Application.Current;

        //真正退出（托盘退出或程序触发）
        if (app.IsQuitting || _isShutdownInProgress)
        {
            if (_isShutdownInProgress)
            {
                return;
            }

            e.Cancel = true;
            _isShutdownInProgress = true;
            Closing -= OnClosing;
            IsEnabled = false;
            _viewModel.StatusText = GetText("Stopping");
            CloseOverlay();
            CloseTaskbarWidget();

            try
            {
                _refreshCancellation.Cancel();

                if (_refreshLoopTask is not null)
                {
                    try
                    {
                        await _refreshLoopTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                if (_orchestrator is not null)
                {
                    await _orchestrator.DisposeAsync();
                }
            }
            finally
            {
                _refreshCancellation.Dispose();
                Close();
            }
            return;
        }

        //关闭到托盘
        if (_settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        //默认行为：完全退出
        app.BeginShutdown();
        e.Cancel = true;
    }

    public void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void NavigateToMonitorPage()
    {
        _viewModel.IsMonitorPageSelected = true;
    }

    private void ApplySettingsPresentation()
    {
        _settings = _settings.Normalize();
        _texts = _localizer.GetTexts(_settings.Language);
        _viewModel.Texts = _texts;
        Title = GetText("AppTitle");
        ThemePalette.Apply(_viewModel.Theme, _settings.ThemeMode);
        ApplyThemeResources();
        RefreshStaticOptions();
        RefreshLocalizedOverlayAndTaskbarOptions();
    }

    private void RefreshLocalizedOverlayAndTaskbarOptions()
    {
        LoadOverlaySettings();
        if (_overlayWindow is not null)
        {
            _overlayWindow.ApplyMetricsFromSettings(_viewModel.OverlaySettings.Metrics);
        }

        LoadTaskbarSettings();
        if (_taskbarWidget is not null)
        {
            _taskbarWidget.ApplyMetricsFromSettings(_viewModel.TaskbarSettings.Metrics);
        }
    }

    private void ApplyThemeResources()
    {
        SetBrushResource("WindowBackgroundBrush", _viewModel.Theme.WindowBackgroundBrush);
        SetBrushResource("WindowChromeBrush", _viewModel.Theme.WindowChromeBrush);
        SetBrushResource("NavigationBackgroundBrush", _viewModel.Theme.NavigationBackgroundBrush);
        SetBrushResource("PageBackgroundBrush", _viewModel.Theme.PageBackgroundBrush);
        SetBrushResource("CardBackgroundBrush", _viewModel.Theme.CardBackgroundBrush);
        SetBrushResource("SecondaryCardBackgroundBrush", _viewModel.Theme.SecondaryCardBackgroundBrush);
        SetBrushResource("BorderBrush", _viewModel.Theme.BorderBrush);
        SetBrushResource("AccentBrush", _viewModel.Theme.AccentBrush);
        SetBrushResource("AccentForegroundBrush", _viewModel.Theme.AccentForegroundBrush);
        SetBrushResource("TextPrimaryBrush", _viewModel.Theme.TextPrimaryBrush);
        SetBrushResource("TextSecondaryBrush", _viewModel.Theme.TextSecondaryBrush);
        SetBrushResource("TextMutedBrush", _viewModel.Theme.TextMutedBrush);
        SetBrushResource("InputBackgroundBrush", _viewModel.Theme.InputBackgroundBrush);
        SetBrushResource("InputBorderBrush", _viewModel.Theme.InputBorderBrush);
        SetBrushResource("SelectedNavigationBackgroundBrush", _viewModel.Theme.SelectedNavigationBackgroundBrush);
        SetBrushResource("HoverNavigationBackgroundBrush", _viewModel.Theme.HoverNavigationBackgroundBrush);
        SetBrushResource("SeparatorBrush", _viewModel.Theme.SeparatorBrush);
    }

    private void SetBrushResource(string key, Brush value)
    {
        Resources[key] = value;
    }

    private void RefreshStaticOptions()
    {
        SyncSelectionOptions(_viewModel.LanguageOptions,
        [
            new SelectionOptionViewModel { Value = LanguageModes.System, Label = GetText("LanguageSystem") },
            new SelectionOptionViewModel { Value = LanguageModes.Chinese, Label = GetText("LanguageChinese") },
            new SelectionOptionViewModel { Value = LanguageModes.English, Label = GetText("LanguageEnglish") }
        ]);

        SyncSelectionOptions(_viewModel.ThemeOptions,
        [
            new SelectionOptionViewModel { Value = ThemeModes.System, Label = GetText("ThemeSystem") },
            new SelectionOptionViewModel { Value = ThemeModes.Light, Label = GetText("ThemeLight") },
            new SelectionOptionViewModel { Value = ThemeModes.Dark, Label = GetText("ThemeDark") }
        ]);

        SyncSelectionOptions(_viewModel.TemperatureUnitOptions,
        [
            new SelectionOptionViewModel { Value = TemperatureUnits.Celsius, Label = GetText("TempCelsius") },
            new SelectionOptionViewModel { Value = TemperatureUnits.Fahrenheit, Label = GetText("TempFahrenheit") }
        ]);
    }

    private void RefreshDeviceOptions(HardwareMetrics snapshot)
    {
        var allLabel = GetText("AllDevices");
        SyncSelectionOptions(_viewModel.GpuOptions,
            new[] { new SelectionOptionViewModel { Value = UiSettings.AllDevicesValue, Label = allLabel } }
                .Concat(snapshot.Gpus.Select(gpu => new SelectionOptionViewModel { Value = gpu.Id, Label = gpu.Name })));

        SyncSelectionOptions(_viewModel.NetworkAdapterOptions,
            new[] { new SelectionOptionViewModel { Value = UiSettings.AllDevicesValue, Label = allLabel } }
                .Concat(snapshot.NetworkAdapters.Select(adapter => new SelectionOptionViewModel { Value = adapter.AdapterId, Label = adapter.AdapterName })));

        var monitoredGpus = FilterSelectedGpus(snapshot.Gpus).ToArray();
        var monitoredAdapters = FilterSelectedAdapters(snapshot.NetworkAdapters).ToArray();

        SyncSelectionOptions(_viewModel.MetricGpuOptions,
            monitoredGpus.Select(gpu => new SelectionOptionViewModel { Value = gpu.Id, Label = gpu.Name }));

        SyncSelectionOptions(_viewModel.MetricNetworkAdapterOptions,
            monitoredAdapters.Select(adapter => new SelectionOptionViewModel { Value = adapter.AdapterId, Label = adapter.AdapterName }));

        NormalizeMetricSourceSelections(monitoredGpus, monitoredAdapters);
    }

    private void NormalizeMetricSourceSelections(
        IReadOnlyList<GpuMetrics> monitoredGpus,
        IReadOnlyList<NetworkAdapterMetrics> monitoredAdapters)
    {
        var resolvedMetricGpuId = ResolveMetricDeviceId(_settings.SelectedGpuId, _settings.MetricGpuId,
            monitoredGpus.Select(static gpu => gpu.Id));
        var resolvedMetricNetworkId = ResolveMetricDeviceId(_settings.SelectedNetworkAdapterId, _settings.MetricNetworkAdapterId,
            monitoredAdapters.Select(static adapter => adapter.AdapterId));

        var changed = false;
        _viewModel.Settings.Changed -= OnSettingsChanged;

        try
        {
            if (!string.Equals(_settings.MetricGpuId, resolvedMetricGpuId, StringComparison.OrdinalIgnoreCase))
            {
                _settings.MetricGpuId = resolvedMetricGpuId;
                _viewModel.Settings.MetricGpuId = resolvedMetricGpuId;
                changed = true;
            }

            if (!string.Equals(_settings.MetricNetworkAdapterId, resolvedMetricNetworkId, StringComparison.OrdinalIgnoreCase))
            {
                _settings.MetricNetworkAdapterId = resolvedMetricNetworkId;
                _viewModel.Settings.MetricNetworkAdapterId = resolvedMetricNetworkId;
                changed = true;
            }
        }
        finally
        {
            _viewModel.Settings.Changed += OnSettingsChanged;
        }

        if (changed)
        {
            _settingsStore.Save(_settings);
        }
    }

    private HardwareMetrics CreateFocusedMetricSnapshot(HardwareMetrics snapshot)
    {
        var metricGpu = ResolveMetricGpu(snapshot.Gpus);
        var metricNetworkAdapter = ResolveMetricNetworkAdapter(snapshot.NetworkAdapters);

        return new HardwareMetrics
        {
            TimestampUtc = snapshot.TimestampUtc,
            Cpu = snapshot.Cpu,
            Gpus = metricGpu is null ? Array.Empty<GpuMetrics>() : new[] { metricGpu },
            Memory = snapshot.Memory,
            Fps = snapshot.Fps,
            NetworkAdapters = metricNetworkAdapter is null ? Array.Empty<NetworkAdapterMetrics>() : new[] { metricNetworkAdapter },
            ActiveMonitors = snapshot.ActiveMonitors,
            IsElevated = snapshot.IsElevated,
            DataSourceInfo = snapshot.DataSourceInfo
        };
    }

    private GpuMetrics? ResolveMetricGpu(IReadOnlyList<GpuMetrics> gpus)
    {
        var monitoredGpus = FilterSelectedGpus(gpus).ToArray();
        if (monitoredGpus.Length == 0)
        {
            return null;
        }

        var resolvedId = ResolveMetricDeviceId(_settings.SelectedGpuId, _settings.MetricGpuId,
            monitoredGpus.Select(static gpu => gpu.Id));
        return monitoredGpus.FirstOrDefault(gpu => gpu.Id.Equals(resolvedId, StringComparison.OrdinalIgnoreCase))
               ?? monitoredGpus[0];
    }

    private NetworkAdapterMetrics? ResolveMetricNetworkAdapter(IReadOnlyList<NetworkAdapterMetrics> adapters)
    {
        var monitoredAdapters = FilterSelectedAdapters(adapters).ToArray();
        if (monitoredAdapters.Length == 0)
        {
            return null;
        }

        var resolvedId = ResolveMetricDeviceId(_settings.SelectedNetworkAdapterId, _settings.MetricNetworkAdapterId,
            monitoredAdapters.Select(static adapter => adapter.AdapterId));
        return monitoredAdapters.FirstOrDefault(adapter => adapter.AdapterId.Equals(resolvedId, StringComparison.OrdinalIgnoreCase))
               ?? monitoredAdapters[0];
    }

    private static string ResolveMetricDeviceId(string monitoredSelectionId, string metricSelectionId, IEnumerable<string> candidateIds)
    {
        var ids = candidateIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
        {
            return UiSettings.AllDevicesValue;
        }

        if (!monitoredSelectionId.Equals(UiSettings.AllDevicesValue, StringComparison.OrdinalIgnoreCase))
        {
            return ids.FirstOrDefault(id => id.Equals(monitoredSelectionId, StringComparison.OrdinalIgnoreCase))
                   ?? ids[0];
        }

        return ids.FirstOrDefault(id => id.Equals(metricSelectionId, StringComparison.OrdinalIgnoreCase))
               ?? ids[0];
    }

    private string FormatCpuUsage(float value) => UiValueFormatter.FormatPercent(value, _settings.CpuUsageDecimals);
    private string FormatGpuUsage(float value) => UiValueFormatter.FormatPercent(value, _settings.GpuUsageDecimals);
    private string FormatMemoryUsage(float value) => UiValueFormatter.FormatPercent(value, _settings.MemoryUsageDecimals);

    private string GetText(string key) => _texts.TryGetValue(key, out var value) ? value : key;

    private static string Fallback(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "N/A" : value;
    }

    private static void SyncMetricRows(ObservableCollection<MetricRowViewModel> target, params (string Label, string Value)[] items)
    {
        SyncCollection(
            target,
            items,
            item => item.Label,
            viewModel => viewModel.Label,
            item => new MetricRowViewModel { Label = item.Label },
            (viewModel, item) => viewModel.Value = item.Value);
    }

    private static void SyncSelectionOptions(ObservableCollection<SelectionOptionViewModel> target, IEnumerable<SelectionOptionViewModel> options)
    {
        SyncCollection(
            target,
            options,
            option => option.Value,
            viewModel => viewModel.Value,
            option => new SelectionOptionViewModel { Value = option.Value },
            (viewModel, option) =>
            {
                viewModel.Value = option.Value;
                viewModel.Label = option.Label;
            });
    }

    private static void SyncCollection<TViewModel, TSource, TKey>(
        ObservableCollection<TViewModel> target,
        IEnumerable<TSource> items,
        Func<TSource, TKey> sourceKeySelector,
        Func<TViewModel, TKey> targetKeySelector,
        Func<TSource, TViewModel> create,
        Action<TViewModel, TSource> update)
        where TKey : notnull
    {
        var materializedItems = items.ToList();

        for (var index = 0; index < materializedItems.Count; index++)
        {
            var sourceItem = materializedItems[index];
            var sourceKey = sourceKeySelector(sourceItem);

            if (index < target.Count &&
                EqualityComparer<TKey>.Default.Equals(targetKeySelector(target[index]), sourceKey))
            {
                update(target[index], sourceItem);
                continue;
            }

            var existingIndex = -1;
            for (var i = index + 1; i < target.Count; i++)
            {
                if (EqualityComparer<TKey>.Default.Equals(targetKeySelector(target[i]), sourceKey))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                target.Move(existingIndex, index);
                update(target[index], sourceItem);
                continue;
            }

            var viewModel = create(sourceItem);
            update(viewModel, sourceItem);
            target.Insert(index, viewModel);
        }

        while (target.Count > materializedItems.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private void OnRestartAsAdmin(object sender, RoutedEventArgs e)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule?.FileName
                           ?? Environment.ProcessPath
                           ?? AppDomain.CurrentDomain.BaseDirectory + "GearGauge.UI.exe",
                UseShellExecute = true,
                Verb = "runas"
            });

            if (process is not null)
            {
                Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            _viewModel.ElevationBadge = $"Elevation failed: {ex.Message}";
            _viewModel.CanElevate = true;
        }
    }

    private void OnShowMonitorPage(object sender, RoutedEventArgs e)
    {
        _viewModel.IsMonitorPageSelected = true;
    }

    private void OnShowSettingsPage(object sender, RoutedEventArgs e)
    {
        _viewModel.IsSettingsPageSelected = true;
    }

    private void OnShowOverlayPage(object sender, RoutedEventArgs e)
    {
        _viewModel.IsOverlayPageSelected = true;
    }

    private void OnShowTaskbarPage(object sender, RoutedEventArgs e)
    {
        _viewModel.IsTaskbarPageSelected = true;
    }

    private void OnSettingsChanged()
    {
        _viewModel.Settings.ApplyTo(_settings);
        _settingsStore.Save(_settings);

        AutoStartHelper.Apply(_settings.AutoStart);
        ApplySettingsPresentation();

        var app = (App)Application.Current;
        app.UpdateTraySettings(_settings, _texts);

        if (_lastSnapshot is not null)
        {
            ApplySnapshot(_lastSnapshot);
        }
    }

    private void ShowToast(string message)
    {
        _toastTimer?.Stop();

        _viewModel.ToastText = message;
        _viewModel.IsToastVisible = true;

        var border = ToastBorder;
        border.RenderTransform = new TranslateTransform(0, 20);

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
        var slideUp = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };

        border.BeginAnimation(FrameworkElement.OpacityProperty, fadeIn);
        border.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            var slideDown = new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (_, _) => _viewModel.IsToastVisible = false;

            border.BeginAnimation(FrameworkElement.OpacityProperty, fadeOut);
            border.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideDown);
        };
        _toastTimer.Start();
    }

    private void LoadOverlaySettings()
    {
        _viewModel.OverlaySettings.Load(_settings, _texts);
        _viewModel.OverlaySettings.Changed -= OnOverlaySettingsChanged;
        _viewModel.OverlaySettings.Changed += OnOverlaySettingsChanged;
    }

    private void OnOverlaySettingsChanged()
    {
        // Apply overlay settings immediately (no save required)
        _viewModel.OverlaySettings.ApplyTo(_settings);
        _settingsStore.Save(_settings);

        // Sync tray menu state
        var app = (App)Application.Current;
        app.UpdateTraySettings(_settings, _texts);

        if (_settings.OverlayEnabled)
        {
            if (_overlayWindow is null)
            {
                ShowOverlayIfNeeded();
            }
            else
            {
                RefreshOverlayLabels();
                _overlayWindow.ApplyMetricsFromSettings(_viewModel.OverlaySettings.Metrics);
                _overlayWindow.ApplyStyle(_settings);
                _overlayWindow.SetLayout(_settings.OverlayEdge, _settings.OverlayAlignment, _settings.OverlayItemSpacing);
            }
        }
        else
        {
            CloseOverlay();
        }
    }

    private void ShowOverlayIfNeeded()
    {
        if (!_settings.OverlayEnabled) return;
        if (_overlayWindow is not null) return;

        _overlayWindow = new OverlayWindow();
        RefreshOverlayLabels();
        _overlayWindow.ApplyMetricsFromSettings(_viewModel.OverlaySettings.Metrics);
        _overlayWindow.ApplyStyle(_settings);
        _overlayWindow.SetLayout(_settings.OverlayEdge, _settings.OverlayAlignment, _settings.OverlayItemSpacing);
        _overlayWindow.Show();

        if (_lastSnapshot is not null)
        {
            _overlayWindow.ApplySnapshot(CreateFocusedMetricSnapshot(_lastSnapshot), _settings, _texts);
        }
    }

    private void RefreshOverlayLabels()
    {
        foreach (var metric in _viewModel.OverlaySettings.Metrics)
        {
            var defaultLabel = GetText($"Overlay_{metric.MetricKey}");
            metric.Label = string.IsNullOrWhiteSpace(metric.CustomLabel)
                ? defaultLabel
                : metric.CustomLabel;
        }
    }

    private void CloseOverlay()
    {
        if (_overlayWindow is null) return;
        _overlayWindow.Close();
        _overlayWindow = null;
    }

    public void ToggleOverlay(bool enabled)
    {
        _settings.OverlayEnabled = enabled;

        // 临时解绑 Changed 事件，防止 VM 属性变更触发 OnSettingsChanged
        // 导致 _viewModel.Settings.ToSettings() 重建 _settings 时丢失 TaskbarWidgetEnabled 等属性
        _viewModel.Settings.Changed -= OnSettingsChanged;
        _viewModel.Settings.OverlayEnabled = enabled;
        _viewModel.Settings.Changed += OnSettingsChanged;

        _viewModel.OverlaySettings.OverlayEnabled = enabled;

        _settingsStore.Save(_settings);

        if (enabled)
        {
            LoadOverlaySettings();
            ShowOverlayIfNeeded();
        }
        else
        {
            CloseOverlay();
        }
    }

    public void ToggleOverlayMetric(string metricKey, bool visible)
    {
        var metric = _viewModel.OverlaySettings.Metrics.FirstOrDefault(m => m.MetricKey == metricKey);
        if (metric is not null)
        {
            metric.IsVisible = visible;
        }

        if (_overlayWindow is not null)
        {
            _overlayWindow.ApplyMetricsFromSettings(_viewModel.OverlaySettings.Metrics);
        }
    }

    private void OnMetricMoveUp(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is OverlayMetricViewModel metric)
        {
            var index = _viewModel.OverlaySettings.Metrics.IndexOf(metric);
            _viewModel.OverlaySettings.MoveMetricUp(index);
        }
    }

    private void OnMetricMoveDown(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is OverlayMetricViewModel metric)
        {
            var index = _viewModel.OverlaySettings.Metrics.IndexOf(metric);
            _viewModel.OverlaySettings.MoveMetricDown(index);
        }
    }

    private void OnApplyOverlayUniformColor(object sender, RoutedEventArgs e)
    {
        var color = OverlayUniformColorPicker?.HexColor;
        if (string.IsNullOrWhiteSpace(color)) return;
        _viewModel.OverlaySettings.ApplyUniformColor(color);
    }

    private void OnApplyTaskbarUniformColor(object sender, RoutedEventArgs e)
    {
        var color = TaskbarUniformColorPicker?.HexColor;
        if (string.IsNullOrWhiteSpace(color)) return;
        _viewModel.TaskbarSettings.ApplyUniformColor(color);
    }

    // --- Taskbar Widget lifecycle ---

    private void LoadTaskbarSettings()
    {
        _viewModel.TaskbarSettings.Load(_settings, _texts);
        _viewModel.TaskbarSettings.Changed -= OnTaskbarSettingsChanged;
        _viewModel.TaskbarSettings.Changed += OnTaskbarSettingsChanged;
    }

    private void OnTaskbarSettingsChanged()
    {
        _viewModel.TaskbarSettings.ApplyTo(_settings);
        _settingsStore.Save(_settings);

        var app = (App)Application.Current;
        app.UpdateTraySettings(_settings, _texts);

        if (_settings.TaskbarWidgetEnabled)
        {
            if (_taskbarWidget == null)
            {
                ShowTaskbarWidgetIfNeeded();
            }
            else
            {
                RefreshTaskbarLabels();
                _taskbarWidget.ApplyMetricsFromSettings(_viewModel.TaskbarSettings.Metrics);
                _taskbarWidget.ApplyStyle(_settings);
                _taskbarWidget.SetRowSpacing(_settings.TaskbarRowSpacing);
            }
        }
        else
        {
            CloseTaskbarWidget();
        }
    }

    private void ShowTaskbarWidgetIfNeeded()
    {
        if (!_settings.TaskbarWidgetEnabled) return;
        if (_taskbarWidget is not null) return;

        RefreshTaskbarLabels();
        _taskbarWidget = new TaskbarWidgetWindow();
        _taskbarWidget.ApplyMetricsFromSettings(_viewModel.TaskbarSettings.Metrics);
        _taskbarWidget.SetRowSpacing(_settings.TaskbarRowSpacing);
        _taskbarWidget.ApplyStyle(_settings);
        if (_lastSnapshot is not null)
        {
            _taskbarWidget.ApplySnapshot(CreateFocusedMetricSnapshot(_lastSnapshot), _settings, _texts);
        }

        _taskbarWidget.Create();
        _taskbarWidget.TryEmbed();
    }

    private void CloseTaskbarWidget()
    {
        if (_taskbarWidget is null) return;
        _taskbarWidget.Dispose();
        _taskbarWidget = null;
    }

    private void RefreshTaskbarLabels()
    {
        foreach (var metric in _viewModel.TaskbarSettings.Metrics)
        {
            var defaultLabel = GetText($"Overlay_{metric.MetricKey}");
            metric.Label = string.IsNullOrWhiteSpace(metric.CustomLabel)
                ? defaultLabel
                : metric.CustomLabel;
        }
    }

    public void ToggleTaskbarWidget(bool enabled)
    {
        _settings.TaskbarWidgetEnabled = enabled;

        _viewModel.TaskbarSettings.TaskbarWidgetEnabled = enabled;

        _settingsStore.Save(_settings);

        if (enabled)
        {
            LoadTaskbarSettings();
            ShowTaskbarWidgetIfNeeded();
        }
        else
        {
            CloseTaskbarWidget();
        }
    }

    private void OnTaskbarMetricMoveUp(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is OverlayMetricViewModel metric)
        {
            var index = _viewModel.TaskbarSettings.Metrics.IndexOf(metric);
            _viewModel.TaskbarSettings.MoveMetricUp(index);
        }
    }

    private void OnTaskbarMetricMoveDown(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is OverlayMetricViewModel metric)
        {
            var index = _viewModel.TaskbarSettings.Metrics.IndexOf(metric);
            _viewModel.TaskbarSettings.MoveMetricDown(index);
        }
    }
}
