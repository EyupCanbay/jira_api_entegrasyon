namespace JiraEntegrasyonApi.Core.Entities
{
    public class JiraTaskLog
    {
        public int Id { get; set; }
        public string JiraKey { get; set; } = string.Empty; // Örn: PROJ-123
        public string Summary { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public string Priority { get; set; } = string.Empty;
        public bool IsResolved { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }
    }
}