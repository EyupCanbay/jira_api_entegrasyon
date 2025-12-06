using JiraEntegrasyonApi.Core.Dtos;
using JiraEntegrasyonApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JiraEntegrasyonApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhookController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("jira-hook")]
        public async Task<IActionResult> HandleWebhook([FromBody] JiraWebhookRoot payload)
        {
            if (payload?.Issue == null) return Ok();

            var key = payload.Issue.Key;
            var status = payload.Issue.Fields?.Status?.Name ?? "Unknown";

            var task = await _context.JiraTaskLogs.FirstOrDefaultAsync(x => x.JiraKey == key);
            
            if (task != null)
            {
                task.Status = status;
                task.LastUpdated = DateTime.UtcNow;

                if (status == "Done" || status == "Tamamlandı")
                {
                    task.IsResolved = true;
                }
                else 
                {
                    task.IsResolved = false;
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"WEBHOOK: {key} güncellendi -> {status}");
            }

            return Ok();
        }
    }
}