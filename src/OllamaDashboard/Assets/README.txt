Hier dein eigenes App-Icon ablegen als `icon.ico`.

Tipps zum Erstellen:
- Tool: GIMP, Paint.NET, oder online https://icoconvert.com
- Größen enthalten: 16×16, 32×32, 48×48, 256×256 (Alpha-Kanal erwünscht)
- Wenn kein Icon vorhanden ist: entferne in `OllamaDashboard.csproj` die Zeile
  `<ApplicationIcon>Assets\icon.ico</ApplicationIcon>`
  und im Inno-Setup-Script die Zeile `SetupIconFile=...`
