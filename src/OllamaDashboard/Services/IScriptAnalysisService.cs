namespace OllamaDashboard.Services;

public enum ContentProfile
{
    FormulaHeavy,
    ProseHeavy,
    Balanced
}

/// <summary>Result of the pre-flight content scan.</summary>
public sealed class PreFlightResult
{
    public required string RecommendedModel { get; init; }
    public required string Reasoning { get; init; }
    public ContentProfile Profile { get; init; }
    public double FormulaDensity { get; init; }
    public double ProseComplexity { get; init; }
}

/// <summary>Style metadata extracted from a reference PDF for prompt injection.</summary>
public sealed class StyleGuideline
{
    public double AverageSectionWords { get; init; }
    public int MaxHeadingDepth { get; init; }
    public double BulletPointRatio { get; init; }
    public bool HasMathContent { get; init; }

    /// <summary>Human-readable instruction injected verbatim into the system prompt.</summary>
    public required string PromptInstruction { get; init; }
}

public interface IScriptAnalysisService
{
    /// <summary>
    /// Heuristic pre-flight scan: classifies content (formula-heavy vs. prose-heavy)
    /// and recommends the best available Ollama model for extraction.
    /// Runs synchronously — no network calls, suitable for calling just before streaming.
    /// </summary>
    PreFlightResult RunPreFlightScan(
        string text,
        IReadOnlyList<string> availableModelNames,
        string currentModel);

    /// <summary>
    /// Analyzes a reference PDF's structure, heading depth, bullet usage, and math content.
    /// Returns a <see cref="StyleGuideline"/> whose <see cref="StyleGuideline.PromptInstruction"/>
    /// is injected into the system prompt so the LLM mimics the reference style.
    /// </summary>
    StyleGuideline AnalyzeReferenceStyle(PdfExtractionResult reference);
}
