using JiraEntegrasyonApi.Core.Dtos;

namespace JiraEntegrasyonApi.Core.Interfaces
{
    public interface IJiraClient
    {
        Task<string> CreateIssueAsync(CreateTaskDto dto);
    }
}