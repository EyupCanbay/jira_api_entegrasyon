namespace JiraEntegrasyonApi.Models
{
    public class KarsiSirketResponse
    {
        public string? EncryptedPayload { get; set; }
    }

    public class CozulmusBilgiler
    {
        public string? JiraEmail { get; set; }
        public string? JiraToken { get; set; }
        public string? ProjectKey { get; set; }
        public string? SprintId { get; set; }
    }
}