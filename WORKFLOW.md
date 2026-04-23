# 🚀 Professioneller Development Workflow

## Überblick

Dieser Workflow verwendet **GitHub Feature Branches + Pull Requests** für alle Änderungen. Der `main` Branch ist immer produktionsreif und geschützt.

---

## 📋 Standard-Workflow (für neue Features/Updates)

### 1. **Feature-Branch erstellen** (Von `main` aus)
```bash
git checkout main
git pull origin main
git checkout -b feature/beschreibung-der-anderung
```

**Branch-Naming-Konvention:**
- `feature/xyz` — Neue Features
- `fix/xyz` — Bugfixes
- `docs/xyz` — Dokumentation
- `refactor/xyz` — Code-Umstrukturierung
- `perf/xyz` — Performance-Verbesserungen

### 2. **Änderungen machen & testen**
```bash
# Änderungen durchführen
# Bauen und testen
dotnet build
dotnet test
```

### 3. **Commits mit aussagekräftigen Nachrichten**
```bash
git add src/OllamaDashboard/...
git commit -m "Fix: UpdateService null reference exception

- Added null checks in CreateManager()
- Improved error message for missing credentials

Fixes #123"
```

**Commit-Format:**
```
<Type>: <Subject>

<Body (optional)>

<Footer (optional, z.B. Fixes #123)>
```

### 4. **Branch zu GitHub pushen**
```bash
git push origin feature/xyz
```

GitHub gibt dir einen Link → Click um **Pull Request zu erstellen**

### 5. **Pull Request (Code Review)**
- Title: Prägnant und aussagekräftig
- Description: Was, Warum, Wie
- GitHub Actions CI/CD läuft automatisch
- **Peer Review** (falls konfiguriert)
- Approve → Merge

### 6. **Nach Merge: Main aktualisieren lokal**
```bash
git checkout main
git pull origin main
```

---

## ✅ Checkliste vor PR

- [ ] Code testet lokal (`dotnet build && dotnet test`)
- [ ] Keine `.vs/`, `bin/`, `obj/`, `publish/` Dateien committed
- [ ] Commits sind logisch strukturiert (1 Commit pro Feature, nicht 20)
- [ ] Commit-Messages sind beschreibend
- [ ] Keine Debug-Logs oder `Console.WriteLine()` im Code
- [ ] Code folgt bestehenden Style-Guidelines

---

## 🔒 Branch Protection Rules (main)

Diese **sollten auf GitHub aktiviert sein**:

1. ✅ Require pull request reviews before merging (mind. 1)
2. ✅ Require status checks to pass (GitHub Actions)
3. ✅ Require branches to be up to date before merging
4. ✅ Dismiss stale pull request approvals
5. ✅ Require code owners review (optional)
6. ✅ Restrict who can push to matching branches

**Einrichten:**
Settings → Branches → Add Rule → Branch name pattern: `main`

---

## 🔄 GitHub Actions CI/CD (Template)

**Datei:** `.github/workflows/build.yml`
```yaml
name: Build & Test

on:
  push:
    branches: [ main, feature/* ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release
```

---

## 📦 Release-Prozess

Sobald alles auf `main` ist:

```bash
# Version erhöhen (semantic versioning)
# z.B. von 0.1.0 zu 0.2.0

.\publish.ps1 -Version "0.2.0" -Upload -GitHubToken "ghp_XXXXX"
```

Dies erstellt:
- ✅ `Setup.exe` (Installer für Erstinstallation)
- ✅ `OllamaDashboard-0.2.0-win-full.nupkg` (Auto-Update Paket)
- ✅ `RELEASES` (Manifest für Update-Checks)
- ✅ GitHub Release mit allen Assets

Auto-Update funktioniert dann vollautomatisch!

---

## 🎯 Zusammenfassung

| Schritt | Befehl |
|---------|--------|
| Branch erstellen | `git checkout -b feature/xyz` |
| Änderungen machen | *Code editieren* |
| Committen | `git commit -m "beschreibung"` |
| Pushen | `git push origin feature/xyz` |
| PR erstellen | GitHub UI oder `gh pr create` |
| Merge (nach Review) | GitHub UI oder `gh pr merge` |
| Main aktualisieren | `git pull origin main` |
| Release bauen | `.\publish.ps1 -Version "X.Y.Z"` |

---

## 🔗 Nützliche Commands

```bash
# PR aus CLI erstellen
gh pr create --title "Mein Feature" --body "Beschreibung"

# PR reviewen & mergen
gh pr view --web
gh pr merge feature/xyz --auto

# Lokale Branches säubern
git branch -d feature/xyz  # Nach Merge

# History anschauen
git log --oneline --graph --all
```

---

## 📞 Kontakt / Issues

- 🐛 Bug gefunden? → GitHub Issues erstellen
- 💡 Idee? → Discussions
- ❓ Frage? → Discussions

Immer **aussagekräftige Titel und Beschreibungen** verwenden!
