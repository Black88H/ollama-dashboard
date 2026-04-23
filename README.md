# Ollama Dashboard v2.1.0

Eine kostenlose Windows-App, mit der du KI-Modelle direkt auf deinem PC nutzen kannst — ganz ohne Internetverbindung und ohne Abo.

**Was kann die App?**
- **PDF Chat** — Lade ein PDF hoch und stelle per Drag & Drop Fragen dazu
- **Skript-Extraktor** — Ziehe prüfungsrelevanten Stoff aus langen Skripten (5 Detailstufen, Export als PDF)
- **Groq-Integration** — Kostenlose Server-KI als Alternative zu lokalen Modellen
- **Auto-Update** — Die App aktualisiert sich selbst über GitHub

---

## Schritt-für-Schritt: Erste Schritte

### Schritt 1 — Ollama installieren

Ollama ist das Programm, das die KI-Modelle auf deinem PC ausführt.

1. Öffne https://ollama.com/download
2. Klicke auf **Download for Windows**
3. Führe die heruntergeladene `.exe`-Datei aus und folge dem Installer
4. Öffne danach die **Eingabeaufforderung** (Windows-Taste → `cmd` eingeben → Enter)
5. Lade dein erstes Modell herunter:
   ```
   ollama pull llama3.1:8b
   ```
   > Dieser Download ist ca. 5 GB groß und läuft einmalig. Danach funktioniert die App offline.

6. Stelle sicher, dass Ollama läuft — im Systray (rechts unten in der Taskleiste) sollte das Ollama-Symbol erscheinen.

---

### Schritt 2 — Ollama Dashboard herunterladen

**Richtige Datei für Windows (64-Bit):**

> Lade **`OllamaDashboard-win-x64.exe`** von der [Releases-Seite](https://github.com/Black88H/ollama-dashboard/releases/latest) herunter.

Falls du unsicher bist, welches System du hast: Windows-Taste → `Einstellungen` → `System` → `Info` → unter **Gerätetyp** steht "64-Bit-Betriebssystem".

---

### Schritt 3 — App starten

1. Doppelklicke auf `OllamaDashboard-win-x64.exe`
2. Falls Windows eine Warnung zeigt ("Unbekannter Herausgeber"): Klicke auf **Weitere Informationen** → **Trotzdem ausführen**
3. Die App startet und zeigt die Chat-Ansicht

> **Hinweis:** Die App benötigt keine Installation. Du kannst die `.exe` überall ablegen.

---

### Schritt 4 — Modell auswählen

In der linken Leiste siehst du oben das aktuell aktive Modell (z. B. "llama3.1:8b").
- Klicke darauf, um ein anderes Modell zu wählen
- Wenn noch kein Modell erscheint: Lade zuerst eines mit `ollama pull <modellname>` (siehe Schritt 1)

---

## Die drei Funktionen

### PDF Chat

1. Klicke auf **PDF auswählen** oder ziehe eine PDF-Datei in den markierten Bereich
2. Warte, bis der Text extrahiert wurde (Seitenanzahl und Token-Schätzung erscheinen)
3. Stelle deine Frage im Eingabefeld unten und drücke **Enter** oder den Sende-Button
4. Die KI antwortet in Echtzeit (Text erscheint Wort für Wort)

> Tipp: Mit **Shift+Enter** kannst du einen Zeilenumbruch im Eingabefeld einfügen, ohne die Nachricht zu senden.

---

### Skript-Extraktor

Ideal um aus langen Uni-Skripten das Prüfungsrelevante herauszufiltern.

1. Klicke auf **PDF auswählen** und wähle dein Skriptum
2. Stelle die **Detailstufe** ein (Schieberegler, 1 = sehr kurz, 5 = ausführlich)
3. Optional: Gib **Fokus-Themen** ein (z. B. "Thermodynamik, Wärmeübertragung")
4. Klicke auf **Extrahieren**
5. Das Ergebnis erscheint auf der rechten Seite — du kannst es als **PDF exportieren**

**Tipp — Groq als schnelle Alternative:**
Wenn du keinen leistungsstarken PC hast, kannst du die Groq API verwenden (kostenlos, kein eigenes Modell nötig). Siehe Abschnitt [Groq API einrichten](#groq-api-einrichten-optional) weiter unten.

---

### Einstellungen

Erreichbar über das Zahnrad-Symbol in der Seitenleiste.

| Einstellung | Erklärung |
|---|---|
| **Base URL** | Adresse von Ollama — normalerweise `http://localhost:11434` |
| **Modell** | Aktuell ausgewähltes KI-Modell |
| **Temperatur** | 0 = sachlich/präzise, 1 = kreativ, 2 = sehr kreativ |
| **Kontextfenster** | Wie viele Tokens (Wörter) die KI im Gedächtnis behält |
| **Theme** | Hell / Dunkel / System |

---

## Groq API einrichten (optional)

Groq ist ein kostenloser Cloud-Dienst mit sehr schnellen KI-Modellen (Llama 3, Mixtral).
Vorteil: Kein lokales Modell nötig, kein langsamer PC erforderlich.

**So richtest du Groq ein:**

1. Gehe zu https://console.groq.com/keys
2. Melde dich kostenlos an (kein Kreditkarte nötig)
3. Klicke auf **Create API Key** und kopiere den Schlüssel
4. Öffne in der App: **Einstellungen → Server-KI — Groq API**
5. Füge den API Key in das Feld **API Key** ein
6. Wähle ein Modell (Empfehlung: `llama-3.3-70b-versatile`)
7. Setze den Haken bei **"Groq für Skript-Extraktor verwenden"**
8. Klicke auf **Einstellungen speichern**

Ab jetzt wird der Skript-Extraktor Groq statt deines lokalen Modells verwenden.

---

## Häufige Probleme

### "Verbindung zu Ollama fehlgeschlagen"

- Stelle sicher, dass Ollama läuft (Systray-Symbol in der Taskleiste rechts unten)
- Falls nicht: Öffne die **Eingabeaufforderung** und führe `ollama serve` aus
- In den Einstellungen: klicke auf **Verbindung testen**

### "Kein Modell installiert"

Öffne die Eingabeaufforderung und führe aus:
```
ollama pull llama3.1:8b
```

### Das PDF wird nicht erkannt

- Die Datei muss eine echte `.pdf`-Datei sein (keine gescannte Bilddatei ohne OCR)
- Sehr alte oder passwortgeschützte PDFs werden möglicherweise nicht unterstützt

### Windows blockiert die App ("SmartScreen-Filter")

Das ist normal bei Apps ohne Signierungszertifikat. Klicke auf **Weitere Informationen** → **Trotzdem ausführen**.

---

## Systemanforderungen

| | Minimum | Empfohlen |
|---|---|---|
| **Betriebssystem** | Windows 10 (64-Bit) | Windows 11 (64-Bit) |
| **RAM** | 8 GB | 16 GB oder mehr |
| **Speicherplatz** | 8 GB frei (für Modell) | 20 GB frei |
| **GPU** | nicht nötig | NVIDIA GPU (schnellere Antworten) |

> Die App selbst ist eine einzelne `.exe`-Datei (~80 MB) und benötigt keine Installation.

---

## Release Build erstellen (für Entwickler)

```powershell
# Self-contained: keine externe .NET Runtime nötig
dotnet publish src/OllamaDashboard/OllamaDashboard.csproj `
    -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true

# Updater
dotnet publish src/OllamaDashboard.Updater/OllamaDashboard.Updater.csproj `
    -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true
```

Ausgabe: `src/OllamaDashboard/bin/Release/net8.0-windows/win-x64/publish/OllamaDashboard.exe`

---

## Projektstruktur (für Entwickler)

```
ollama-dashboard/
├── OllamaDashboard.sln
└── src/
    ├── OllamaDashboard/                  # Haupt-App (WPF / .NET 8)
    │   ├── Models/                       # Datenmodelle (AppSettings, ChatMessage, Groq DTOs)
    │   ├── Services/                     # OllamaService, GroqService, PdfService, UpdateService
    │   ├── ViewModels/                   # MVVM (CommunityToolkit.Mvvm)
    │   ├── Views/                        # XAML: MainWindow, Chat, ScriptExtractor, Settings
    │   └── Themes/                       # Light/Dark Theme Ressourcen
    └── OllamaDashboard.Updater/          # Self-Update Helfer
```

**Technologie-Stack:**

| Schicht | Tech |
|---|---|
| UI | WPF / XAML (.NET 8) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.Hosting |
| PDF-Extraktion | PdfPig (Apache 2.0) |
| PDF-Export | QuestPDF (Community License) |
| KI lokal | Ollama REST API |
| KI Server | Groq API (OpenAI-kompatibel) |
| Logging | Serilog → `%AppData%\OllamaDashboard\logs\` |
| Updates | Velopack + GitHub Releases |

---

## Lizenz

- **QuestPDF** — Community License (kostenlos für Einzelpersonen & Open Source): https://www.questpdf.com/license/
- **PdfPig** — Apache 2.0
- **CommunityToolkit.Mvvm** — MIT
- **Ollama-Modelle** — je nach Modell unterschiedlich (Llama, Mistral, Gemma); bitte eigenständig prüfen
