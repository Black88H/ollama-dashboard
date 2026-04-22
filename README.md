# Ollama Dashboard

Eine schlanke, minimalistische Windows-Desktop-App (WPF / .NET 8), die lokal mit [Ollama](https://ollama.com) kommuniziert und drei Kernfunktionen bietet:

1. **PDF Chat** — PDF per Drag & Drop laden, Fragen stellen, Streaming-Antwort
2. **Skript-Extraktor** — prüfungsrelevanten Stoff aus Skripten ziehen, per Slider in 5 Detailstufen, Export als neue PDF
3. **Self-Update** — automatischer Update-Check via GitHub Releases API mit anschließendem Self-Update

---

## Projektstruktur

```
ollama-dashboard/
├── OllamaDashboard.sln
├── src/
│   ├── OllamaDashboard/                       # Haupt-App (WPF)
│   │   ├── App.xaml(.cs)                      # DI-Bootstrap
│   │   ├── app.manifest                       # Windows/DPI settings
│   │   ├── appsettings.json                   # Default-Config
│   │   ├── Models/                            # ChatMessage, AppSettings, Ollama DTOs, UpdateInfo
│   │   ├── Services/                          # OllamaService, PdfService, UpdateService, SettingsService
│   │   ├── ViewModels/                        # MVVM (CommunityToolkit.Mvvm)
│   │   ├── Views/                             # MainWindow + ChatView + ScriptExtractorView + SettingsView
│   │   ├── Themes/                            # Colors.xaml, Styles.xaml
│   │   ├── Converters/                        # WPF IValueConverters
│   │   └── Assets/                            # icon.ico (hier einsetzen!)
│   └── OllamaDashboard.Updater/               # Mini-Helfer für Self-Update
├── installer/
│   └── setup.iss                              # Inno Setup Script
└── docs/                                      # Dokumentation
```

---

## Voraussetzungen

- **Windows 10 / 11** (x64)
- **.NET 8 SDK** → https://dotnet.microsoft.com/download/dotnet/8.0
- **Ollama** läuft lokal → https://ollama.com/download
  - Standard-URL: `http://localhost:11434`
  - Empfohlenes Start-Modell: `ollama pull llama3.1:8b` (~5 GB)
- Für den Installer: **Inno Setup 6.2+** → https://jrsoftware.org/isdl.php

---

## Erster Start in Cursor

```bash
# 1. Repo klonen / Dateien entpacken
cd ollama-dashboard

# 2. Abhängigkeiten holen
dotnet restore

# 3. Lauf!
dotnet run --project src/OllamaDashboard
```

Beim ersten Start wird unter `%AppData%\OllamaDashboard\settings.json` eine Default-Konfig angelegt. Dort kannst du später direkt Hand anlegen, oder bequem über Einstellungen in der App.

### Was du **vor dem ersten Build** anpassen solltest

- `src/OllamaDashboard/appsettings.json` → `Update.GitHubOwner` + `Update.GitHubRepo` auf dein eigenes Repo setzen
- `src/OllamaDashboard/OllamaDashboard.csproj` → `<Company>` und `<Copyright>` anpassen
- `src/OllamaDashboard/Assets/icon.ico` → eigenes Icon hinterlegen (sonst schlägt der Build fehl → alternativ `<ApplicationIcon>`-Zeile entfernen)
- `installer/setup.iss` → `MyAppPublisher`, `MyAppURL` und die GUID `AppId` ändern

---

## Release + Update-Flow

### 1. Release-Build erstellen

```powershell
# Haupt-App publizieren (self-contained:false → .NET Runtime wird vorausgesetzt)
dotnet publish src/OllamaDashboard/OllamaDashboard.csproj `
    -c Release -r win-x64 --no-self-contained `
    /p:PublishSingleFile=false

# Updater publizieren
dotnet publish src/OllamaDashboard.Updater/OllamaDashboard.Updater.csproj `
    -c Release -r win-x64 --no-self-contained
```

### 2. Installer bauen (optional, für Endnutzer)

```powershell
# Inno Setup Compiler
iscc installer\setup.iss
# → dist/OllamaDashboard-Setup-0.1.0.exe
```

### 3. GitHub Release anlegen

1. Version in `OllamaDashboard.csproj` bumpen (`<Version>0.2.0</Version>`)
2. Publish-Output der Haupt-App als ZIP packen (z. B. `OllamaDashboard-0.2.0.zip`)
3. Git Tag: `git tag v0.2.0 && git push --tags`
4. Auf GitHub: neues Release mit Tag `v0.2.0` anlegen, ZIP als Asset anhängen
5. Die installierten Apps finden das Update beim nächsten Check automatisch

### Update-Mechanik intern

```
[App.exe läuft] ── "Nach Updates suchen" ──▶ GitHub API (latest release)
                                                     │
                                                     ▼
                                         Version-Vergleich (semver)
                                                     │
                                         Update verfügbar → Download ZIP
                                                     │
                                         Entpacken nach %TEMP%\OllamaDashboardUpdate\
                                                     │
                                         Updater.exe starten + App.exe beenden
                                                     │
                                                     ▼
                          [Updater wartet 500ms] ──▶ Kopiert Dateien über Install-Folder
                                                     │
                                                     ▼
                                         App.exe neu gestartet
```

---

## Architektur-Notizen

| Schicht | Tech | Bemerkung |
|---|---|---|
| UI | WPF / XAML | Minimal-Design über `Themes/Colors.xaml` + `Themes/Styles.xaml` — dort anpassen |
| MVVM | CommunityToolkit.Mvvm | `[ObservableProperty]` + `[RelayCommand]` Source-Generators |
| DI | Microsoft.Extensions.Hosting | Bootstrap in `App.xaml.cs` |
| HTTP | `HttpClientFactory` | Geteilter `HttpClient` pro Service |
| PDF-Text | PdfPig | MIT, funktioniert offline, `ContentOrderTextExtractor` für sauberen Lesefluss |
| PDF-Export | QuestPDF | MIT (Community-License), fluent API |
| Logs | Serilog | `%AppData%\OllamaDashboard\logs\app-YYYYMMDD.log` |
| Versioning | Semver | Für Tag-Vergleiche |

### Wichtige Erweiterungs-Punkte

**RAG statt Context-Stuffing.** Für sehr große PDFs (>40 000 Zeichen) schneidet `ChatViewModel.TruncateForContext` aktuell hart ab. Für Production: Chunking + Embeddings + Vector-Search. Ollama bietet `/api/embeddings`. Als lokaler Vector-Store eignet sich [`LiteDB`](https://www.litedb.org/) oder [`Qdrant`](https://qdrant.tech/) (als separater Container).

**Authentifizierte GitHub-Releases.** Wenn du private Releases willst, erweitere `UpdateService`: GitHub Personal Access Token in den Auth-Header setzen. Achtung: Token nicht in der App einbetten — abfragbar über Login-Dialog oder Azure Key Vault.

**Code Signing.** Für professionellen Einsatz: Setup.exe + OllamaDashboard.exe mit einem Code-Signing-Zertifikat signieren (`signtool sign`). SmartScreen beschwert sich sonst bei Endnutzern.

**Dark Mode.** `AppSettings.Theme` ist bereits vorbereitet. Ein zweites `Colors.Dark.xaml` anlegen und beim Theme-Wechsel tauschen via `Application.Current.Resources.MergedDictionaries`.

---

## Lizenz-Hinweis

- **QuestPDF** nutzt die Community-License → kostenlos für Einzelpersonen, Startups (≤1 M USD Umsatz) und Open-Source. Bei kommerziellem Einsatz ggf. Lizenz prüfen: https://www.questpdf.com/license/
- **PdfPig** — Apache 2.0, freie Nutzung
- **CommunityToolkit.Mvvm** — MIT
- **Ollama-Modelle** haben je nach Modell eigene Lizenzen (Llama, Mistral, Gemma — alle unterschiedlich) — prüfen!
