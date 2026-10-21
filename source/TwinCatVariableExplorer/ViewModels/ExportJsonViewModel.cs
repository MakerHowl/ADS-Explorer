using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TwinCAT.TypeSystem;
using TwinCatVariableExplorer.Services;

namespace TwinCatVariableExplorer.ViewModels;

public partial class ExportJsonViewModel : ObservableObject
{
    private readonly IAdsPlcService _plcService;
    private readonly ISymbolJsonExportService _exportService;
    private ISymbol? _resolvedSymbol;

    [ObservableProperty]
    private string _symbolPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _suggestedPaths = new();

    [ObservableProperty]
    private string _jsonPreview = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canSave;

    public ExportJsonViewModel(IAdsPlcService plcService, ISymbolJsonExportService exportService, ISymbol? initialSymbol = null)
    {
        _plcService = plcService ?? throw new ArgumentNullException(nameof(plcService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));

        if (initialSymbol != null)
        {
            _resolvedSymbol = initialSymbol;
            SymbolPath = initialSymbol.InstancePath;

            // Collect symbol and its parent hierarchy for convenient selection
            var current = initialSymbol;
            while (current != null)
            {
                if (!string.IsNullOrEmpty(current.InstancePath))
                {
                    SuggestedPaths.Add(current.InstancePath);
                }
                current = current.Parent;
            }
        }
    }

    partial void OnSymbolPathChanged(string value)
    {
        _ = RefreshPreviewAsync();
    }

    [RelayCommand]
    public async Task RefreshPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(SymbolPath))
        {
            JsonPreview = string.Empty;
            StatusMessage = "Bitte einen Symbolpfad angeben.";
            CanSave = false;
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Lese Symbol und erstelle Vorschau...";

            // Resolve symbol from PLC service or use already resolved if path matches
            ISymbol? symbol = (_resolvedSymbol != null && string.Equals(_resolvedSymbol.InstancePath, SymbolPath, StringComparison.OrdinalIgnoreCase))
                ? _resolvedSymbol
                : _plcService.GetSymbol(SymbolPath);

            if (symbol == null)
            {
                JsonPreview = string.Empty;
                StatusMessage = $"Symbol '{SymbolPath}' wurde nicht in der SPS gefunden.";
                CanSave = false;
                return;
            }

            _resolvedSymbol = symbol;
            string json = await _exportService.SerializeSymbolToJsonAsync(symbol, indented: true);
            JsonPreview = json;
            StatusMessage = $"Vorschau für '{symbol.InstancePath}' ({symbol.TypeName}) erfolgreich erstellt ({json.Length} Zeichen).";
            CanSave = true;
        }
        catch (Exception ex)
        {
            JsonPreview = string.Empty;
            StatusMessage = $"Fehler beim Erstellen der JSON-Vorschau: {ex.Message}";
            CanSave = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveToFileAsync()
    {
        if (_resolvedSymbol == null || string.IsNullOrEmpty(JsonPreview))
        {
            StatusMessage = "Kein gültiges Symbol zum Speichern vorhanden.";
            return;
        }

        string defaultFileName = $"{SanitizeFileName(_resolvedSymbol.InstanceName)}.json";
        var dialog = new SaveFileDialog
        {
            Title = "Symbol als JSON exportieren",
            Filter = "JSON-Dateien (*.json)|*.json|Alle Dateien (*.*)|*.*",
            DefaultExt = ".json",
            FileName = defaultFileName
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Speichere JSON-Datei...";
                await _exportService.ExportSymbolToFileAsync(_resolvedSymbol, dialog.FileName, indented: true);
                StatusMessage = $"Datei erfolgreich gespeichert: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Speichern: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    public void CopyToClipboard()
    {
        if (string.IsNullOrEmpty(JsonPreview))
        {
            StatusMessage = "Keine Daten zum Kopieren vorhanden.";
            return;
        }

        try
        {
            Clipboard.SetText(JsonPreview);
            StatusMessage = "JSON erfolgreich in die Zwischenablage kopiert!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Kopieren in die Zwischenablage: {ex.Message}";
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "symbol";

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
