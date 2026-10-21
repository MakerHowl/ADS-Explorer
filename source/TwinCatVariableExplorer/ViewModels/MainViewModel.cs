using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TwinCAT.Ads;
using TwinCatVariableExplorer.Services;

namespace TwinCatVariableExplorer.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAdsPlcService _plcService;
    private readonly ISymbolJsonExportService _exportService;
    private readonly object _variablesLock = new();

    [ObservableProperty]
    private string _amsNetId = "127.0.0.1.1.1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    private int _amsPort = 851;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusText = "Nicht verbunden";

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isGlobalOnChangeEnabled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ObservableCollection<PlcVariableItemViewModel> _variables = new();

    [ObservableProperty]
    private PlcVariableItemViewModel? _selectedVariable;

    [ObservableProperty]
    private string _itemCountSummary = "0 Variablen";

    public ICollectionView VariablesView { get; }

    public string ConnectButtonText => IsConnected ? "Trennen" : "Verbinden";

    public MainViewModel() : this(new AdsPlcService(), new SymbolJsonExportService())
    {
    }

    public MainViewModel(IAdsPlcService plcService) : this(plcService, new SymbolJsonExportService())
    {
    }

    public MainViewModel(IAdsPlcService plcService, ISymbolJsonExportService exportService)
    {
        _plcService = plcService ?? throw new ArgumentNullException(nameof(plcService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _plcService.ConnectionStateChanged += OnPlcConnectionStateChanged;
        IsConnected = _plcService.IsConnected;

        BindingOperations.EnableCollectionSynchronization(Variables, _variablesLock);
        VariablesView = CollectionViewSource.GetDefaultView(Variables);
        VariablesView.Filter = FilterVariables;
    }

    private bool FilterVariables(object item)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        if (item is PlcVariableItemViewModel vm)
        {
            return (vm.InstancePath?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (vm.SymbolName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (vm.DataTypeName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (vm.Comment?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return false;
    }

    partial void OnFilterTextChanged(string value)
    {
        VariablesView.Refresh();
        UpdateItemCountSummary();
    }

    partial void OnIsGlobalOnChangeEnabledChanged(bool value)
    {
        if (value)
        {
            var visibleItems = VariablesView.Cast<PlcVariableItemViewModel>().ToList();
            const int maxLimit = 500;
            int countToActivate = Math.Min(visibleItems.Count, maxLimit);

            for (int i = 0; i < countToActivate; i++)
            {
                visibleItems[i].IsOnChangeActive = true;
            }

            if (visibleItems.Count > maxLimit)
            {
                StatusText = $"Hinweis: Auto-Update auf die ersten {maxLimit} von {visibleItems.Count} sichtbaren Variablen limitiert (Router-Schutz).";
            }
            else
            {
                StatusText = $"Auto-Update für {countToActivate} sichtbare Variablen aktiviert.";
            }
        }
        else
        {
            foreach (var variable in Variables)
            {
                variable.IsOnChangeActive = false;
            }

            StatusText = "Auto-Update für alle Variablen deaktiviert.";
        }
    }

    [RelayCommand]
    public async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            await DisconnectAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            IsBusy = true;
            StatusText = $"Verbinde mit {AmsNetId}:{AmsPort}...";

            await _plcService.ConnectAsync(AmsNetId, AmsPort);
            IsConnected = _plcService.IsConnected;
            StatusText = $"Verbunden mit {AmsNetId}:{AmsPort} ({_plcService.CurrentPlcState}) - Lade Symbole...";

            await ReloadSymbolsInternalAsync();
        }
        catch (AdsErrorException adsEx)
        {
            IsConnected = false;
            StatusText = $"ADS-Fehler beim Verbinden: {adsEx.Message} (Error 0x{adsEx.ErrorCode:X})";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = $"Verbindungsfehler: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            IsBusy = true;
            StatusText = "Trenne Verbindung...";
            IsGlobalOnChangeEnabled = false;
            ClearVariables();
            _plcService.Disconnect();
            IsConnected = false;
            StatusText = "Verbindung getrennt.";
        }
        finally
        {
            IsBusy = false;
            UpdateItemCountSummary();
        }
    }

    [RelayCommand]
    public async Task ReloadSymbolsAsync()
    {
        if (!IsConnected)
        {
            StatusText = "Nicht verbunden. Symbole können nicht geladen werden.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Lade Symbole aus der SPS...";
            await ReloadSymbolsInternalAsync();
        }
        catch (AdsErrorException adsEx)
        {
            StatusText = $"ADS-Fehler beim Laden: {adsEx.Message} (0x{adsEx.ErrorCode:X})";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler beim Laden der Symbole: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadSymbolsInternalAsync()
    {
        IsGlobalOnChangeEnabled = false;
        ClearVariables();

        var symbols = await _plcService.LoadAllValueSymbolsAsync();
        lock (_variablesLock)
        {
            foreach (var sym in symbols)
            {
                Variables.Add(new PlcVariableItemViewModel(sym));
            }
        }

        VariablesView.Refresh();
        UpdateItemCountSummary();

        StatusText = $"Verbunden mit {AmsNetId}:{AmsPort} ({_plcService.CurrentPlcState}) - {Variables.Count} Variablen geladen. Lese initiale Werte...";
        await ReadAllVisibleValuesInternalAsync();
        StatusText = $"Verbunden mit {AmsNetId}:{AmsPort} ({_plcService.CurrentPlcState}) - {Variables.Count} Variablen geladen.";
    }

    [RelayCommand]
    public async Task ReadAllVisibleValuesAsync()
    {
        if (!IsConnected)
        {
            StatusText = "Nicht verbunden.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Lese Werte sichtbarer Variablen...";
            await ReadAllVisibleValuesInternalAsync();
            StatusText = $"Werte erfolgreich gelesen ({DateTime.Now:HH:mm:ss}).";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler beim Lesen der Werte: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void OpenExportJsonDialog()
    {
        if (!IsConnected)
        {
            StatusText = "Nicht verbunden. JSON-Export erfordert eine aktive SPS-Verbindung.";
            return;
        }

        var vm = new ExportJsonViewModel(_plcService, _exportService, SelectedVariable?.Symbol);
        var win = new Views.ExportJsonWindow(vm);
        win.ShowDialog();
    }

    [RelayCommand]
    public async Task QuickExportSelectedSymbolJsonAsync()
    {
        if (!IsConnected)
        {
            StatusText = "Nicht verbunden. JSON-Export erfordert eine aktive SPS-Verbindung.";
            return;
        }

        var symbol = SelectedVariable?.Symbol;
        if (symbol == null)
        {
            StatusText = "Bitte zuerst eine Variable im Grid auswählen.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = $"Symbol '{symbol.InstanceName}' als JSON speichern",
            Filter = "JSON-Dateien (*.json)|*.json|Alle Dateien (*.*)|*.*",
            DefaultExt = ".json",
            FileName = $"{symbol.InstanceName}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsBusy = true;
                StatusText = $"Exportiere '{symbol.InstancePath}' nach JSON...";
                await _exportService.ExportSymbolToFileAsync(symbol, dialog.FileName, indented: true);
                StatusText = $"Symbol '{symbol.InstancePath}' erfolgreich exportiert nach: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"Fehler beim JSON-Export: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private async Task ReadAllVisibleValuesInternalAsync()
    {
        var visibleItems = VariablesView.Cast<PlcVariableItemViewModel>().ToList();
        await Task.Run(() =>
        {
            foreach (var item in visibleItems)
            {
                item.ReadCurrentValue();
            }
        });
    }

    private void UpdateItemCountSummary()
    {
        int total = Variables.Count;
        int visible = 0;
        foreach (var _ in VariablesView)
        {
            visible++;
        }

        ItemCountSummary = string.IsNullOrWhiteSpace(FilterText)
            ? $"{total} Variablen"
            : $"{visible} von {total} Variablen angezeigt";
    }

    private void ClearVariables()
    {
        lock (_variablesLock)
        {
            foreach (var item in Variables)
            {
                item.Dispose();
            }
            Variables.Clear();
        }
    }

    private void OnPlcConnectionStateChanged(object? sender, AdsState state)
    {
        void Apply()
        {
            IsConnected = _plcService.IsConnected;
            if (!IsConnected)
            {
                StatusText = $"Verbindung getrennt (SPS-Status: {state}).";
            }
            else
            {
                StatusText = $"Verbunden mit {AmsNetId}:{AmsPort} ({state}) - {Variables.Count} Variablen geladen.";
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke((Action)Apply);
        }
        else
        {
            Apply();
        }
    }

    public void Dispose()
    {
        _plcService.ConnectionStateChanged -= OnPlcConnectionStateChanged;
        ClearVariables();
        _plcService.Dispose();
        GC.SuppressFinalize(this);
    }
}
