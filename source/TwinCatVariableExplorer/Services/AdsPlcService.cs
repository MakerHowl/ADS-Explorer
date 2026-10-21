using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.Ads.TypeSystem;
using TwinCAT.TypeSystem;
using TwinCAT.ValueAccess;

namespace TwinCatVariableExplorer.Services;

public class AdsPlcService : IAdsPlcService
{
    private readonly AdsClient _adsClient;
    private AdsState _currentPlcState = AdsState.Invalid;
    private ISymbolLoader? _symbolLoader;
    private bool _disposed;

    public AdsPlcService()
    {
        _adsClient = new AdsClient();
        _adsClient.AdsStateChanged += OnAdsStateChanged;
        _adsClient.ConnectionStateChanged += OnClientConnectionStateChanged;
    }

    public bool IsConnected => _adsClient.IsConnected;

    public AdsState CurrentPlcState => _currentPlcState;

    public ISymbolLoader? SymbolLoader => _symbolLoader;

    public event EventHandler<AdsState>? ConnectionStateChanged;

    public async Task ConnectAsync(string amsNetId, int port)
    {
        Disconnect();

        await Task.Run(() =>
        {
            _adsClient.Connect(amsNetId, port);
            StateInfo state = _adsClient.ReadState();
            _currentPlcState = state.AdsState;
        });

        ConnectionStateChanged?.Invoke(this, _currentPlcState);
    }

    public void Disconnect()
    {
        if (_adsClient.IsConnected)
        {
            _adsClient.Disconnect();
        }

        _symbolLoader = null;
        _currentPlcState = AdsState.Invalid;
        ConnectionStateChanged?.Invoke(this, _currentPlcState);
    }

    public async Task<IReadOnlyList<IValueSymbol>> LoadAllValueSymbolsAsync()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("ADS-Client ist nicht mit einer SPS verbunden.");
        }

        return await Task.Run<IReadOnlyList<IValueSymbol>>(() =>
        {
            var settings = new SymbolLoaderSettings(SymbolsLoadMode.VirtualTree, ValueAccessMode.SymbolicByHandle);
            _symbolLoader = SymbolLoaderFactory.Create(_adsClient, settings);

            var result = new List<IValueSymbol>();
            CollectLeafValueSymbols(_symbolLoader.Symbols, result);
            return result.AsReadOnly();
        });
    }

    public ISymbol? GetSymbol(string instancePath)
    {
        if (!IsConnected || _symbolLoader == null || string.IsNullOrWhiteSpace(instancePath))
            return null;

        if (_symbolLoader.Symbols.TryGetInstance(instancePath, out ISymbol? symbol))
        {
            return symbol;
        }

        return FindSymbolRecursive(_symbolLoader.Symbols, instancePath);
    }

    private ISymbol? FindSymbolRecursive(IEnumerable<ISymbol> symbols, string instancePath)
    {
        foreach (var sym in symbols)
        {
            if (string.Equals(sym.InstancePath, instancePath, StringComparison.OrdinalIgnoreCase))
                return sym;

            if (sym.SubSymbols != null && sym.SubSymbols.Count > 0 &&
                instancePath.StartsWith(sym.InstancePath + ".", StringComparison.OrdinalIgnoreCase))
            {
                var found = FindSymbolRecursive(sym.SubSymbols, instancePath);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void CollectLeafValueSymbols(IEnumerable<ISymbol> symbols, List<IValueSymbol> result)
    {
        foreach (var sym in symbols)
        {
            if (sym.SubSymbols != null && sym.SubSymbols.Count > 0)
            {
                CollectLeafValueSymbols(sym.SubSymbols, result);
            }
            else if (sym is IValueSymbol valSym)
            {
                if (!sym.IsContainerType() &&
                    sym.Category != DataTypeCategory.Program &&
                    sym.Category != DataTypeCategory.FunctionBlock &&
                    sym.Category != DataTypeCategory.Struct &&
                    sym.Category != DataTypeCategory.Function)
                {
                    result.Add(valSym);
                }
            }
        }
    }

    private void OnAdsStateChanged(object? sender, AdsStateChangedEventArgs e)
    {
        _currentPlcState = e.State.AdsState;
        ConnectionStateChanged?.Invoke(this, _currentPlcState);
    }

    private void OnClientConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.NewState != ConnectionState.Connected)
        {
            _currentPlcState = AdsState.Invalid;
            ConnectionStateChanged?.Invoke(this, _currentPlcState);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _adsClient.AdsStateChanged -= OnAdsStateChanged;
        _adsClient.ConnectionStateChanged -= OnClientConnectionStateChanged;
        Disconnect();
        _adsClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
