using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace OllamaDashboard.Services;

public sealed partial class ScriptAnalysisService : IScriptAnalysisService
{
    private readonly ILogger<ScriptAnalysisService> _logger;

    public ScriptAnalysisService(ILogger<ScriptAnalysisService> logger)
    {
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Pre-flight scan
    // -------------------------------------------------------------------------

    public PreFlightResult RunPreFlightScan(
        string text,
        IReadOnlyList<string> availableModelNames,
        string currentModel)
    {
        var formulaDensity = ComputeFormulaDensity(text);
        var proseComplexity = ComputeProseComplexity(text);

        ContentProfile profile;
        string reasoning;
        string recommendedModel;

        if (formulaDensity > 0.04)
        {
            profile = ContentProfile.FormulaHeavy;
            reasoning =
                $"Hohe Formeldichte erkannt ({formulaDensity:P0} des Textes enthält mathematische " +
                "Zeichen/LaTeX). Ein mathematikorientiertes Modell erzielt hier bessere Ergebnisse.";
            recommendedModel = FindBestModel(availableModelNames, currentModel,
                ["qwen2.5-math", "mathstral", "wizard-math", "deepseek-r1", "deepseek"]);
        }
        else if (proseComplexity > 0.60)
        {
            profile = ContentProfile.ProseHeavy;
            reasoning =
                "Hohe Textkomplexität erkannt (langer Satzbau, umfangreiches Vokabular). " +
                "Ein sprachstarkes Modell ist für diese Art von Prosa besser geeignet.";
            recommendedModel = FindBestModel(availableModelNames, currentModel,
                ["mistral", "llama3", "gemma3", "gemma", "phi4", "phi", "command-r"]);
        }
        else
        {
            profile = ContentProfile.Balanced;
            reasoning = "Ausgewogener Inhalt — dein aktives Modell ist für dieses Dokument gut geeignet.";
            recommendedModel = currentModel;
        }

        _logger.LogInformation(
            "Pre-flight: formulaDensity={F:P1}, proseComplexity={P:P1}, profile={Profile}, model={Model}",
            formulaDensity, proseComplexity, profile, recommendedModel);

        return new PreFlightResult
        {
            RecommendedModel = recommendedModel,
            Reasoning = reasoning,
            Profile = profile,
            FormulaDensity = formulaDensity,
            ProseComplexity = proseComplexity
        };
    }

    // -------------------------------------------------------------------------
    // Reference-style analysis
    // -------------------------------------------------------------------------

    public StyleGuideline AnalyzeReferenceStyle(PdfExtractionResult reference)
    {
        var lines = reference.FullText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var headingCount = lines.Count(l => HeadingRegex().IsMatch(l.Trim()));
        var bulletCount  = lines.Count(l =>
        {
            var t = l.TrimStart();
            return t.StartsWith('-') || t.StartsWith('•') || t.StartsWith('*');
        });
        var mathCount = lines.Count(l =>
            l.Contains('$') || MathSymbolRegex().IsMatch(l) || LaTexCommandRegex().IsMatch(l));

        var maxDepth = lines
            .Select(l => CountLeadingHashes(l.Trim()))
            .DefaultIfEmpty(0)
            .Max();

        var bulletRatio = lines.Length > 0 ? (double)bulletCount / lines.Length : 0;
        var hasMath     = lines.Length > 0 && (double)mathCount / lines.Length > 0.05;

        var wordCount      = reference.FullText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var sectionCount   = Math.Max(1, headingCount);
        var avgSectionWords = (double)wordCount / sectionCount;

        var instruction = BuildStyleInstruction(avgSectionWords, maxDepth, bulletRatio, hasMath);

        _logger.LogInformation(
            "Reference style: avgWords/section={W:F0}, headingDepth={D}, bulletRatio={B:P0}, hasMath={M}",
            avgSectionWords, maxDepth, bulletRatio, hasMath);

        return new StyleGuideline
        {
            AverageSectionWords = avgSectionWords,
            MaxHeadingDepth     = maxDepth,
            BulletPointRatio    = bulletRatio,
            HasMathContent      = hasMath,
            PromptInstruction   = instruction
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildStyleInstruction(
        double avgWords, int headingDepth, double bulletRatio, bool hasMath)
    {
        var parts = new List<string>();

        if (avgWords < 80)
            parts.Add("Halte jeden Abschnitt sehr kurz und kompakt (ca. ≤ 80 Wörter)");
        else if (avgWords < 250)
            parts.Add("Verwende mittellange Abschnitte (ca. 80–250 Wörter)");
        else
            parts.Add("Schreibe ausführliche, detaillierte Abschnitte (ca. 250+ Wörter)");

        parts.Add(headingDepth >= 2
            ? "Gliedere zweistufig mit `# Hauptthema` und `## Unterabschnitt`"
            : "Verwende nur eine Gliederungsebene (`# Hauptthema`)");

        parts.Add(bulletRatio > 0.3
            ? "Bevorzuge Aufzählungspunkte (`- Punkt`) statt Fließtext"
            : "Bevorzuge Fließtext statt Aufzählungspunkte");

        if (hasMath)
            parts.Add("Behalte mathematische Ausdrücke und Formeln im Originalformat");

        return string.Join("; ", parts) + ".";
    }

    private static double ComputeFormulaDensity(string text)
    {
        if (text.Length == 0) return 0;

        var mathCharCount = text.Count(c =>
            c is '∑' or '∫' or '≠' or '≤' or '≥' or '√' or 'π'
             or 'Σ' or 'Ω' or 'α' or 'β' or 'γ' or 'δ' or 'ε'
             or 'λ' or 'μ' or 'θ' or 'φ' or 'ψ');
        var dollarCount  = text.Count(c => c == '$');
        var latexCount   = LaTexCommandRegex().Matches(text).Count;
        var equalsInMath = MathEquationRegex().Matches(text).Count;

        return Math.Min(1.0,
            (mathCharCount + dollarCount * 2 + latexCount * 5 + equalsInMath) / (double)text.Length);
    }

    private static double ComputeProseComplexity(string text)
    {
        if (text.Length == 0) return 0;

        var sentences = text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length == 0) return 0;

        var avgSentenceLen = sentences.Average(s =>
            s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var uniqueRatio = words.Length > 0
            ? (double)words
                .Select(w => w.ToLowerInvariant().Trim('.', ',', ';', ':', '!', '?'))
                .Distinct().Count() / words.Length
            : 0;

        // 25-word average sentences = max sentence score; 0.7 unique-word ratio = max vocab score
        var sentenceScore = Math.Min(1.0, avgSentenceLen / 25.0);
        var vocabScore    = Math.Min(1.0, uniqueRatio / 0.7);

        return (sentenceScore + vocabScore) / 2.0;
    }

    private static string FindBestModel(
        IReadOnlyList<string> available, string fallback, IEnumerable<string> preferredHints)
    {
        foreach (var hint in preferredHints)
        {
            var match = available.FirstOrDefault(m =>
                m.Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return fallback;
    }

    private static int CountLeadingHashes(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == '#') count++;
            else break;
        }
        return count;
    }

    // Source-generated regexes — compiled once, zero allocations on repeated calls.
    [GeneratedRegex(@"^#{1,3}\s+\w", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"[∑∫≠≤≥√πΣΩαβγδεζηθλμξρσφχψω]")]
    private static partial Regex MathSymbolRegex();

    [GeneratedRegex(@"\\[a-zA-Z]{2,}")]
    private static partial Regex LaTexCommandRegex();

    // Matches "lhs = rhs" patterns (at least 3 chars on both sides to avoid noise)
    [GeneratedRegex(@"\w{2,}\s*=\s*\w{2,}")]
    private static partial Regex MathEquationRegex();
}
