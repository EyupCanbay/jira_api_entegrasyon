using System.Text.Json.Serialization;

namespace JiraEntegrasyonApi.Core.Dtos
{
    // Jira'dan gelen JSON çok karışıtırıp sadece ihtiyacımız olanları alıyoruz
    public class JiraWebhookRoot
    {
        [JsonPropertyName("issue")]
        public JiraIssue? Issue { get; set; }
    }

    public class JiraIssue
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("fields")]
        public JiraIssueFields? Fields { get; set; }
    }

    public class JiraIssueFields
    {
        [JsonPropertyName("status")]
        public JiraStatus? Status { get; set; }
    }

    public class JiraStatus
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}