# TwinCAT ADS Variable Explorer

Eine moderne, performante WPF-Desktopanwendung zur Diagnose, tabellarischen Anzeige, Echtzeitüberwachung, Bearbeitung und zum rekursiven JSON-Export von SPS-Variablen einer Beckhoff TwinCAT-Steuerung über das ADS-Protokoll.

---

## 🚀 Kernfunktionen

- **Verbindungsmanagement:**
  - Schnelle Verbindung zu lokalen oder entfernten TwinCAT 3 Runtimes über `AmsNetId` (z. B. `127.0.0.1.1.1`) und `AmsPort` (Standard `851`).
  - Visueller Verbindungs- und Statusindikator in der Statusleiste.
  - Fehlertoleranter Verbindungsaufbau mit sauberer Fehlerbehandlung ohne Anwendungsabsturz bei nicht erreichbarer SPS.

- **Symbol-Browsing & Metadaten:**
  - Automatisches Auslesen des SPS-Symbolbaums über den TwinCAT `SymbolLoader` (`VirtualTree`, `SymbolicByHandle`).
  - Intelligentes Flattening verschachtelter Strukturen (`STRUCT`), Arrays und Funktionsbausteine (`FB`) zur direkten Anzeige aller steuerbaren Blattelemente.
  - Anzeige aller relevanten Metadaten: Symbolpfad, Kurzname, SPS-Datentyp, Typ-Kategorie, Speichergröße in Bytes, Kommentar und SPS-Attribute.

- **Tabellarische Darstellung (WPF DataGrid):**
  - Vollständige UI-Virtualisierung (Zeilen- und Spaltenvirtualisierung im Recycling-Modus) für flüssige Bedienung selbst bei tausenden SPS-Variablen.
  - Reaktive Textfilterung (Case-Insensitive) nach Symbolpfad, Variablenname, Datentyp oder Kommentar inklusive Live-Zähler (gefilterte vs. geladene Variablen).

- **Variablen editieren & typsicher schreiben:**
  - Direktes Editieren von Variablenwerten im Grid (`UpdateSourceTrigger=LostFocus`).
  - Robuste Eingabekonvertierung für:
    - Booleans (`true` / `false`, `1` / `0`)
    - Gleitkommazahlen (unterstützt deutsches Komma `,` sowie Punkt `.`)
    - Ganzzahlen (signed/unsigned, 8–64 Bit) inkl. TwinCAT-Hexadezimalschreibweise (`16#FF`) und C#-Notation (`0xFF`)
    - Enums und Strings
  - Automatischer Schutz vor Endlosschleifen zwischen UI und SPS.
  - Schreibfehler oder Schreibschutz-Verletzungen werden visualisiert und der echte SPS-Wert wird automatisch wiederhergestellt.

- **Automatische Aktualisierung (ADS On-Change):**
  - Schaltbare Echtzeit-Überwachung per ADS Device Notifications (`OnChange`) – sowohl einzeln pro Zeile im DataGrid als auch global über einen Hauptschalter.
  - Integriertes Schutzlimit für den globalen Modus (maximal 500 gleichzeitige Notifications), um den ADS-Router vor Überlastung zu schützen.
  - Vollständige Thread-Sicherheit durch Dispatcher-Synchronisation aller ADS-Events auf den UI-Thread.

- **Rekursiver JSON-Export:**
  - Export von Variablen, komplexen Strukturen, Arrays und Funktionsbausteinen in standardkonformes JSON im Key-Value-Format:
    ```json
    {
      "stMachine": {
        "bRunning": true,
        "nSpeed": 1500,
        "stSub": {
          "fTemperature": 42.5
        },
        "aReadings": [10, 20, 30]
      }
    }
    ```
  - **Interaktiver Export-Dialog:**
    - Dropdown mit Eltern-Hierarchie (z. B. vom ausgewählten Feldelement über Unterstrukturen bis zum Hauptbaustein) oder freier Pfadeingabe.
    - Echtzeit-Vorschau des formatierten JSON mit Syntax-Monospace-Schrift (`Consolas`).
    - Ein-Klick-Aktionen: *In Datei speichern...* (`SaveFileDialog`) und *In Zwischenablage kopieren*.
  - **Schnellexport:** Direkter Dateiexport per Rechtsklick-Kontextmenü im DataGrid.

---

## 🛠️ Technologie-Stack

- **Framework:** .NET 10.0 (`net10.0-windows`)
- **UI-Framework:** Windows Presentation Foundation (WPF)
- **Architektur:** MVVM (Model-View-ViewModel)
- **Sprachversion:** C# 12 (`Nullable = enable`, `ImplicitUsings = enable`)
- **NuGet-Pakete:**
  - [`Beckhoff.TwinCAT.Ads`](https://www.nuget.org/packages/Beckhoff.TwinCAT.Ads/) (Version `7.0.*`)
  - [`CommunityToolkit.Mvvm`](https://www.nuget.org/packages/CommunityToolkit.Mvvm/) (Version `8.4.*`)

---

## 📁 Verzeichnis- und Projektstruktur

```text
ads-explorer/
├── .gitignore
├── LICENSE
├── README.md
└── source/
    ├── TwinCatVariableExplorer.sln
    └── TwinCatVariableExplorer/
        ├── TwinCatVariableExplorer.csproj
        ├── App.xaml
        ├── App.xaml.cs
        ├── AssemblyInfo.cs
        ├── Converters/
        │   └── BoolToBrushConverter.cs
        ├── Models/
        │   └── PlcConnectionInfo.cs
        ├── Services/
        │   ├── IAdsPlcService.cs
        │   ├── AdsPlcService.cs
        │   ├── ISymbolJsonExportService.cs
        │   └── SymbolJsonExportService.cs
        ├── ViewModels/
        │   ├── PlcVariableItemViewModel.cs
        │   ├── ExportJsonViewModel.cs
        │   └── MainViewModel.cs
        └── Views/
            ├── MainWindow.xaml
            ├── MainWindow.xaml.cs
            ├── ExportJsonWindow.xaml
            └── ExportJsonWindow.xaml.cs
```

---

## ⚙️ Voraussetzungen

1. **Betriebssystem:** Windows 10 oder Windows 11 (x64)
2. **.NET SDK:** [.NET 10.0 SDK](https://dotnet.microsoft.com/download) oder höher
3. **TwinCAT Runtime:**
   - Beckhoff TwinCAT 3 (XAE oder XAR) lokal installiert **oder**
   - Ein aktiver TwinCAT ADS-Router mit konfigurierter Route zur Ziel-SPS.

---

## 💻 Kompilieren & Starten

### 1. Repository klonen
```bash
git clone https://github.com/MakerHowl/ADS-Explorer.git
cd ADS-Explorer/source
```

### 2. Projektmappe bauen
```bash
dotnet build TwinCatVariableExplorer.sln
```

### 3. Anwendung ausführen
```bash
dotnet run --project TwinCatVariableExplorer
```

---

## 📖 Bedienung

1. **Verbinden:**
   - Trage die `AmsNetId` (z. B. `127.0.0.1.1.1` für lokale SPS) und den `Port` (Standard `851` für erste SPS-Runtime) in der oberen Toolbar ein.
   - Klicke auf **Verbinden**. Die Symbole werden automatisch ausgelesen und die aktuellen Werte angezeigt.
2. **Filtern:**
   - Gib im Filterfeld einen Suchbegriff ein (z. B. `MAIN`, `Sensor`, `BOOL`), um die Anzeige in Echtzeit zu filtern.
3. **Wert ändern:**
   - Klicke doppelt in die Spalte **Wert (Editierbar)**, passe den Wert an und drücke `Enter` oder verlasse die Zelle (`LostFocus`). Der Wert wird typsicher an die SPS gesendet.
4. **Auto-Update aktivieren:**
   - Aktiviere die Checkbox in der Toolbar für globale Benachrichtigungen oder setze den Haken in der Spalte **Auto (On-Change)** für einzelne Variablen.
5. **Symbol als JSON exportieren:**
   - **Variante A:** Markiere eine Variable und klicke in der Toolbar auf **Als JSON exportieren...**. Wähle im Dialog bei Bedarf eine übergeordnete Struktur aus, prüfe die Live-Vorschau und speichere die Datei.
   - **Variante B:** Rechtsklick auf eine beliebige Tabellenzeile $\rightarrow$ **Als JSON exportieren...** oder **Schnellexport als JSON...**.

---

## 📄 Lizenz

Dieses Projekt ist unter der [MIT-Lizenz](LICENSE) lizenziert.
