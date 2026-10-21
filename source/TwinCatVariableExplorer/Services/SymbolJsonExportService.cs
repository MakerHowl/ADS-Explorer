using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using TwinCAT.TypeSystem;

namespace TwinCatVariableExplorer.Services;

public class SymbolJsonExportService : ISymbolJsonExportService
{
    public object? ReadSymbolValueRecursively(ISymbol symbol)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ReadSymbolValueInternal(symbol, visited);
    }

    private object? ReadSymbolValueInternal(ISymbol symbol, HashSet<string> visited)
    {
        if (symbol == null) return null;

        if (!string.IsNullOrEmpty(symbol.InstancePath))
        {
            if (!visited.Add(symbol.InstancePath))
            {
                return "<Zirkuläre Referenz>";
            }
        }

        if (symbol.SubSymbols != null && symbol.SubSymbols.Count > 0)
        {
            if (symbol.Category == DataTypeCategory.Array)
            {
                var list = new List<object?>();
                foreach (var sub in symbol.SubSymbols)
                {
                    list.Add(ReadSymbolValueInternal(sub, visited));
                }
                return list;
            }
            else
            {
                var dict = new Dictionary<string, object?>();
                foreach (var sub in symbol.SubSymbols)
                {
                    dict[sub.InstanceName] = ReadSymbolValueInternal(sub, visited);
                }
                return dict;
            }
        }

        if (symbol is IValueSymbol valSym)
        {
            try
            {
                object raw = valSym.ReadValue();
                return SanitizeValueForJson(raw);
            }
            catch (Exception ex)
            {
                return $"<Fehler beim Lesen: {ex.Message}>";
            }
        }

        return null;
    }

    public Dictionary<string, object?> SerializeSymbolToDictionary(ISymbol symbol)
    {
        var value = ReadSymbolValueRecursively(symbol);
        return new Dictionary<string, object?>
        {
            [symbol.InstanceName] = value
        };
    }

    public string SerializeSymbolToJson(ISymbol symbol, bool indented = true)
    {
        var dict = SerializeSymbolToDictionary(symbol);
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(dict, options);
    }

    public async Task<string> SerializeSymbolToJsonAsync(ISymbol symbol, bool indented = true)
    {
        return await Task.Run(() => SerializeSymbolToJson(symbol, indented));
    }

    public async Task ExportSymbolToFileAsync(ISymbol symbol, string filePath, bool indented = true)
    {
        string json = await SerializeSymbolToJsonAsync(symbol, indented);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    private static object? SanitizeValueForJson(object? raw)
    {
        if (raw == null) return null;

        if (raw is IEnumValue enumVal)
        {
            return enumVal.Name;
        }

        if (raw is TimeSpan ts)
        {
            return ts.ToString();
        }

        if (raw is DateTime dt)
        {
            return dt.ToString("o");
        }

        if (raw is DateTimeOffset dto)
        {
            return dto.ToString("o");
        }

        return raw;
    }
}
