namespace OllamaDashboard.Models;

// ── Enumerations ───────────────────────────────────────────────────────────────

public enum LicenseTier { Free, Pro }

public enum FlashcardDifficulty { Again = 0, Hard = 1, Good = 2, Easy = 3 }

public enum StudyEventType
{
    DailyLogin       = 10,
    ChatMessageSent  =  5,
    PdfAnalyzed      = 20,
    ScriptExtracted  = 25,
    FlashcardReviewed = 3
}

// ── Entities ───────────────────────────────────────────────────────────────────

public class User
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "Lernender";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int XpPoints { get; set; }
    public int StreakDays { get; set; }
    public DateTime? LastStudyDate { get; set; }
    public LicenseTier Tier { get; set; } = LicenseTier.Free;
    public string? LicenseKey { get; set; }

    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();

    // Computed (not persisted)
    public int Level => (int)Math.Sqrt(XpPoints / 100.0);
    public int XpForNextLevel => (Level + 1) * (Level + 1) * 100;
    public double LevelProgress => XpPoints >= XpForNextLevel
        ? 100
        : (XpPoints - Level * Level * 100) * 100.0 / (XpForNextLevel - Level * Level * 100);
}

public class Subject
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ColorHex { get; set; } = "#6366F1";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
}

public class ChatSession
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public string Title { get; set; } = "Neue Sitzung";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }

    public ICollection<PersistedChatMessage> Messages { get; set; } = new List<PersistedChatMessage>();
}

public class PersistedChatMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class Flashcard
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int ReviewCount { get; set; }
    // SM-2 spaced repetition fields
    public double EaseFactor { get; set; } = 2.5;
    public int IntervalDays { get; set; } = 1;
    public DateTime? NextReviewDate { get; set; }
    public FlashcardDifficulty LastRating { get; set; } = FlashcardDifficulty.Good;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
