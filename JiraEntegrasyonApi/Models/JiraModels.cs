namespace JiraEntegrasyonApi.Models
{
    // string yerine string? yazıyoruz
    public class TaskRequestDto
    {
        public string? HataBasligi { get; set; }
        public string? Detay { get; set; }
        public string? Modul { get; set; }
        public string? Oncelik { get; set; }
    }

    public class JiraPayload
    {
        public JiraFields? fields { get; set; }
    }

    public class JiraFields
    {
        public JiraKey? project { get; set; }
        public string? summary { get; set; }
        public JiraName? issuetype { get; set; }
        public JiraDescription? description { get; set; }
    }

    public class JiraKey { public string? key { get; set; } }
    public class JiraName { public string? name { get; set; } }

    public class JiraDescription
    {
        public string type { get; set; } = "doc";
        public int version { get; set; } = 1;
        public List<JiraContent>? content { get; set; }
    }

    public class JiraContent
    {
        public string type { get; set; } = "paragraph";
        public List<JiraTextContent>? content { get; set; }
    }

    public class JiraTextContent
    {
        public string type { get; set; } = "text";
        public string? text { get; set; }
    }
}