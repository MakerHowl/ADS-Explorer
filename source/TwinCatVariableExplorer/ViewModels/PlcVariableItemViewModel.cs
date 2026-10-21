using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;

namespace TwinCatVariableExplorer.ViewModels;

public partial class PlcVariableItemViewModel : ObservableObject, IDisposable
{
    private readonly IValueSymbol _symbol;
    private bool _isUpdatingFromPlc;
    private bool _isSubscribed;

    public PlcVariableItemViewModel(IValueSymbol symbol)
    {
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        try
        {
            _symbol.NotificationSettings = new NotificationSettings(AdsTransMode.OnChange, 100, 0);
        }
        catch
        {
            // In case NotificationSettings cannot be applied to this symbol type
        }
    }

    public IValueSymbol Symbol => _symbol;

    public string InstancePath => _symbol.InstancePath;

    public string SymbolName => _symbol.InstanceName;

    public string DataTypeName => _symbol.TypeName;

    public string Category => _symbol.Category.ToString();

    public int ByteSize => _symbol.ByteSize;

    public string Comment => _symbol.Comment ?? string.Empty;

    public string AttributesSummary => _symbol.Attributes != null && _symbol.Attributes.Count > 0
        ? string.Join(", ", _symbol.Attributes.Select(a => $"{a.Name}={a.Value}"))
        : string.Empty;

    public bool IsReadOnly => _symbol.IsReadOnly;

    [ObservableProperty]
    private object? _value;

    [ObservableProperty]
    private DateTime? _lastUpdated;

    [ObservableProperty]
    private bool _isOnChangeActive;

    [ObservableProperty]
    private string? _errorMessage;

    partial void OnValueChanged(object? value)
    {
        if (_isUpdatingFromPlc)
            return;

        if (IsReadOnly)
        {
            ErrorMessage = "Variable ist schreibgeschützt.";
            ReadCurrentValue(preserveErrorMessage: true);
            return;
        }

        try
        {
            object converted = ConvertValue(value);
            _symbol.WriteValue(converted);
            ErrorMessage = null;
            LastUpdated = DateTime.Now;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Schreibfehler: {ex.Message}";
            ReadCurrentValue(preserveErrorMessage: true);
        }
    }

    partial void OnIsOnChangeActiveChanged(bool value)
    {
        if (value && !_isSubscribed)
        {
            _symbol.ValueChanged += OnSymbolValueChanged;
            _isSubscribed = true;
        }
        else if (!value && _isSubscribed)
        {
            _symbol.ValueChanged -= OnSymbolValueChanged;
            _isSubscribed = false;
        }
    }

    private void OnSymbolValueChanged(object? sender, ValueChangedEventArgs e)
    {
        void Apply()
        {
            try
            {
                _isUpdatingFromPlc = true;
                Value = e.Value;
                LastUpdated = e.DateTime.LocalDateTime;
                ErrorMessage = null;
            }
            finally
            {
                _isUpdatingFromPlc = false;
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

    public void ReadCurrentValue(bool preserveErrorMessage = false)
    {
        try
        {
            var val = _symbol.ReadValue();

            void Apply()
            {
                try
                {
                    _isUpdatingFromPlc = true;
                    Value = val;
                    LastUpdated = DateTime.Now;
                    if (!preserveErrorMessage)
                    {
                        ErrorMessage = null;
                    }
                }
                finally
                {
                    _isUpdatingFromPlc = false;
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
        catch (Exception ex)
        {
            void ApplyError()
            {
                ErrorMessage = $"Lesefehler: {ex.Message}";
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)ApplyError);
            }
            else
            {
                ApplyError();
            }
        }
    }

    private object ConvertValue(object? input)
    {
        if (input == null)
        {
            throw new ArgumentException("Eingabewert darf nicht null sein.");
        }

        Type? targetType = (_symbol.DataType as IManagedMappableType)?.ManagedType;
        if (targetType == null)
        {
            targetType = GetFallbackType(_symbol.TypeName);
        }

        if (targetType == null || input.GetType() == targetType)
        {
            return input;
        }

        string str = input.ToString()?.Trim() ?? string.Empty;

        if (targetType == typeof(bool))
        {
            if (str == "1" || str.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
            if (str == "0" || str.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
            if (bool.TryParse(str, out bool bVal)) return bVal;
            throw new FormatException($"Ungültiger boolescher Wert '{str}'. Erwartet: TRUE/FALSE oder 1/0.");
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, str, ignoreCase: true);
        }

        if (targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal))
        {
            string normalized = str.Replace(',', '.');
            return Convert.ChangeType(normalized, targetType, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(byte) || targetType == typeof(sbyte) ||
            targetType == typeof(short) || targetType == typeof(ushort) ||
            targetType == typeof(int) || targetType == typeof(uint) ||
            targetType == typeof(long) || targetType == typeof(ulong))
        {
            if (str.StartsWith("16#", StringComparison.OrdinalIgnoreCase))
            {
                string hex = str.Substring(3);
                ulong uVal = Convert.ToUInt64(hex, 16);
                return Convert.ChangeType(uVal, targetType, CultureInfo.InvariantCulture);
            }
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string hex = str.Substring(2);
                ulong uVal = Convert.ToUInt64(hex, 16);
                return Convert.ChangeType(uVal, targetType, CultureInfo.InvariantCulture);
            }
            return Convert.ChangeType(str, targetType, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(str, targetType, CultureInfo.InvariantCulture);
    }

    private static Type? GetFallbackType(string typeName)
    {
        return (typeName.ToUpperInvariant()) switch
        {
            "BOOL" => typeof(bool),
            "BYTE" or "USINT" => typeof(byte),
            "SINT" => typeof(sbyte),
            "WORD" or "UINT" => typeof(ushort),
            "INT" => typeof(short),
            "DWORD" or "UDINT" => typeof(uint),
            "DINT" => typeof(int),
            "LWORD" or "ULINT" => typeof(ulong),
            "LINT" => typeof(long),
            "REAL" => typeof(float),
            "LREAL" => typeof(double),
            "STRING" or "WSTRING" => typeof(string),
            _ => null
        };
    }

    public void Dispose()
    {
        if (_isSubscribed)
        {
            _symbol.ValueChanged -= OnSymbolValueChanged;
            _isSubscribed = false;
        }
    }
}
