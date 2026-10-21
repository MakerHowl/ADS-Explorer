using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;

namespace TwinCatVariableExplorer.Services;

public interface IAdsPlcService : IDisposable
{
    bool IsConnected { get; }
    AdsState CurrentPlcState { get; }
    ISymbolLoader? SymbolLoader { get; }
    event EventHandler<AdsState>? ConnectionStateChanged;
    Task ConnectAsync(string amsNetId, int port);
    void Disconnect();
    Task<IReadOnlyList<IValueSymbol>> LoadAllValueSymbolsAsync();
    ISymbol? GetSymbol(string instancePath);
}
