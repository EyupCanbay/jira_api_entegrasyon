namespace JiraEntegrasyonApi.Core.Interfaces
{
    public record JiraCredentials(string Email, string Token, string ProjectKey);

    public interface ISecurityService
    {
        Task<JiraCredentials> GetJiraCredentialsAsync();
    }
}