# 🤝 Contributing Guide

Vielen Dank für dein Interesse, zu **Ollama Dashboard** beizutragen!

---

## 📖 Wie du beitragen kannst

### 🐛 Bug Reports
1. Öffne ein [GitHub Issue](https://github.com/Black88H/ollama-dashboard/issues)
2. Nutze den "Bug Report" Template
3. Beschreibe:
   - Was ist passiert?
   - Was war erwartet?
   - Wie kann ich es reproduzieren?
   - Dein OS, .NET Version, App-Version

### ✨ Feature Requests
1. Öffne ein [GitHub Discussion](https://github.com/Black88H/ollama-dashboard/discussions)
2. Beschreibe:
   - Welche Funktionalität fehlt?
   - Warum brauchst du sie?
   - Wie könnte es implementiert werden?

### 💻 Code Beitragen

**Voraussetzungen:**
- Git installiert
- .NET 8.0 SDK
- Visual Studio 2022 oder VS Code + C# Extension
- GitHub Account

**Schritte:**

1. **Fork das Repository** (oben rechts auf GitHub)

2. **Clone dein Fork lokal:**
   ```bash
   git clone https://github.com/DEIN_USERNAME/ollama-dashboard.git
   cd ollama-dashboard
   ```

3. **Upstream hinzufügen** (um up-to-date zu bleiben):
   ```bash
   git remote add upstream https://github.com/Black88H/ollama-dashboard.git
   ```

4. **Erstelle einen Feature-Branch:**
   ```bash
   git checkout -b feature/mein-feature
   ```

5. **Mache deine Änderungen:**
   - Code schreiben
   - Teste lokal: `dotnet build` und `dotnet test`
   - Keine `bin/`, `obj/`, `.vs/` Dateien committieren

6. **Committen mit klarer Nachricht:**
   ```bash
   git commit -m "feat: Add awesome feature

   - Erklärung was gemacht wurde
   - Warum diese Änderung
   - Fixes #123 (wenn relevant)"
   ```

7. **Push zu deinem Fork:**
   ```bash
   git push origin feature/mein-feature
   ```

8. **Erstelle einen Pull Request:**
   - Gehe zu GitHub
   - Klick auf "Compare & Pull Request"
   - Nutze den PR-Template (erscheint automatisch)
   - Beschreibe deine Änderungen ausführlich

9. **Review & Merge:**
   - Antworte auf Feedback
   - Apporte Änderungen an
   - Nach Genehmigung wird dein PR gemergt

---

## 📐 Code-Style Guidelines

### C# / .NET

**Naming:**
```csharp
// Klassen, Methods: PascalCase
public class UpdateService { }
public void CheckForUpdates() { }

// Properties: PascalCase
public string CurrentVersion { get; }

// Private Fields: _camelCase
private string _settingsPath;

// lokale Variablen: camelCase
var isInstalled = mgr.IsInstalled;
```

**Struktur:**
```csharp
// 1. Using Statements
using System;
using Microsoft.Extensions.DependencyInjection;

// 2. Namespace
namespace OllamaDashboard.Services;

// 3. Klasse
public sealed class MyService
{
    // 3.1 Properties
    public string Name { get; set; }

    // 3.2 Constructor
    public MyService() { }

    // 3.3 Public Methods
    public void DoSomething() { }

    // 3.4 Private Methods
    private void HelperMethod() { }
}
```

**Best Practices:**
- `async/await` verwenden statt `.Result`
- Null-checks mit `??` oder `is not null`
- Keine hardcoded Werte, verwende Settings/Config
- Aussagekräftige Variablennamen (`user` nicht `u`, `settings` nicht `s`)
- Dokumentation für Public APIs: `/// <summary>`
- Logging verwenden statt `Console.WriteLine()`

### XAML / UI

- Verwende `{Binding}` mit `INotifyPropertyChanged`
- Style-Keys: `Key="PrimaryBrush"` (nicht `Key="btn1"`)
- Keine Code-Behind Logic, verwende ViewModels
- Responsive Design testen

---

## 🧪 Testing

Alle öffentlichen Features sollten Tests haben:

```csharp
[TestClass]
public class UpdateServiceTests
{
    [TestMethod]
    public async Task CheckForUpdate_WithoutRepo_ReturnsError()
    {
        // Arrange
        var service = new UpdateService(...);

        // Act
        var result = await service.CheckForUpdateAsync();

        // Assert
        Assert.IsFalse(result.UpdateAvailable);
    }
}
```

**Teste immer lokal vor dem Commit:**
```bash
dotnet test --configuration Release
```

---

## 📋 Commit-Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat:` Neue Feature
- `fix:` Bugfix
- `docs:` Dokumentation
- `style:` Formatierung (keine Code-Logik Änderung)
- `refactor:` Code-Umstrukturierung
- `perf:` Performance-Verbesserung
- `test:` Tests hinzufügen/ändern
- `chore:` Build, Dependencies, etc.

**Beispiele:**
```bash
# Feature
git commit -m "feat(update): Add pre-release update checking"

# Bugfix
git commit -m "fix(ui): Fix null reference in SettingsViewModel

Fixes #456"

# Refactor
git commit -m "refactor(service): Extract UpdateManager creation logic"
```

---

## 🔍 Pull Request Checklist

Bevor du deinen PR submitest:

- [ ] Code lokal gebaut (`dotnet build`)
- [ ] Tests passen (`dotnet test`)
- [ ] Keine `bin/`, `obj/`, `.vs/` Dateien
- [ ] Aussagekräftige Commit-Messages
- [ ] PR-Template ausgefüllt
- [ ] Keine Console.WriteLine() Debugs
- [ ] Code-Style eingehalten
- [ ] Dokumentation aktualisiert

---

## 🚀 Release-Prozess (Maintainer only)

1. Update Version in `.csproj`
2. Erstelle Git Tag: `git tag v1.2.0`
3. Pushe Tag: `git push origin v1.2.0`
4. Nutze: `.\publish.ps1 -Version "1.2.0" -Upload`
5. GitHub Release wird automatisch erstellt

---

## 📞 Fragen?

- 💬 [GitHub Discussions](https://github.com/Black88H/ollama-dashboard/discussions)
- 🐛 [GitHub Issues](https://github.com/Black88H/ollama-dashboard/issues)

---

**Danke für deine Unterstützung! 🎉**
