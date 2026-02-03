namespace LanzaTuIdea.Api.Models;

public class IdeaHistory
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    public DateTime ChangedAt { get; set; }
    public int ChangedByUserId { get; set; }
    public AppUser ChangedByUser { get; set; } = null!;
    public string ChangeType { get; set; } = "";
    public string? Notes { get; set; }
}
