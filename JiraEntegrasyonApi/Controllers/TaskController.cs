using JiraEntegrasyonApi.Core.Dtos;
using JiraEntegrasyonApi.Core.Entities;
using JiraEntegrasyonApi.Core.Interfaces;
using JiraEntegrasyonApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace JiraEntegrasyonApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly IJiraClient _jiraClient;
        private readonly AppDbContext _context;

        public TaskController(IJiraClient jiraClient, AppDbContext context)
        {
            _jiraClient = jiraClient;
            _context = context;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto request)
        {
            try
            {
                // 1. Jira'ya Gönder
                var jiraKey = await _jiraClient.CreateIssueAsync(request);

                // 2. DB'ye Kaydet
                var log = new JiraTaskLog
                {
                    JiraKey = jiraKey,
                    Summary = request.HataBasligi,
                    Priority = request.Oncelik,
                    Status = "To Do",
                    CreatedAt = DateTime.UtcNow
                };

                _context.JiraTaskLogs.Add(log);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Task oluşturuldu", Key = jiraKey });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}