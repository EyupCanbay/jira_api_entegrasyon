using JiraEntegrasyonApi.Models;
using JiraEntegrasyonApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace JiraEntegrasyonApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JiraController : ControllerBase
    {
        private readonly JiraService _jiraService;

        public JiraController(JiraService jiraService)
        {
            _jiraService = jiraService;
        }

        [HttpPost("create-task")]
        public async Task<IActionResult> CreateTask([FromBody] TaskRequestDto request)
        {
            try
            {
                // Basit bir validasyon
                if (string.IsNullOrEmpty(request.HataBasligi))
                    return BadRequest("Hata başlığı boş olamaz.");

                var result = await _jiraService.CreateTaskAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Gerçek hayatta loglama yapılır
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}