using System.Collections.Generic;
using System.Threading.Tasks;
using TwinCAT.TypeSystem;

namespace TwinCatVariableExplorer.Services;

public interface ISymbolJsonExportService
{
    object? ReadSymbolValueRecursively(ISymbol symbol);
    Dictionary<string, object?> SerializeSymbolToDictionary(ISymbol symbol);
    string SerializeSymbolToJson(ISymbol symbol, bool indented = true);
    Task<string> SerializeSymbolToJsonAsync(ISymbol symbol, bool indented = true);
    Task ExportSymbolToFileAsync(ISymbol symbol, string filePath, bool indented = true);
}
