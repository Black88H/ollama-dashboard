using Microsoft.EntityFrameworkCore;
using OllamaDashboard.Models;

namespace OllamaDashboard.Data;

public class StudyCoachDbContext : DbContext
{
    private readonly string _connectionString;

    public StudyCoachDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite(_connectionString);

    public DbSet<User>                 Users         { get; set; } = null!;
    public DbSet<Subject>              Subjects      { get; set; } = null!;
    public DbSet<ChatSession>          ChatSessions  { get; set; } = null!;
    public DbSet<PersistedChatMessage> ChatMessages  { get; set; } = null!;
    public DbSet<Flashcard>            Flashcards    { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>()
             .HasMany(u => u.Subjects)
             .WithOne(s => s.User)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);

        model.Entity<Subject>()
             .HasMany(s => s.ChatSessions)
             .WithOne(cs => cs.Subject)
             .HasForeignKey(cs => cs.SubjectId)
             .OnDelete(DeleteBehavior.Cascade);

        model.Entity<Subject>()
             .HasMany(s => s.Flashcards)
             .WithOne(f => f.Subject)
             .HasForeignKey(f => f.SubjectId)
             .OnDelete(DeleteBehavior.Cascade);

        model.Entity<ChatSession>()
             .HasMany(cs => cs.Messages)
             .WithOne(m => m.Session)
             .HasForeignKey(m => m.SessionId)
             .OnDelete(DeleteBehavior.Cascade);

        // Store enums as integers
        model.Entity<User>()
             .Property(u => u.Tier)
             .HasConversion<int>();

        model.Entity<Flashcard>()
             .Property(f => f.LastRating)
             .HasConversion<int>();
    }
}
