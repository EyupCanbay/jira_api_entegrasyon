using JiraEntegrasyonApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- BİZİM EKLEDİĞİMİZ KISIM BAŞLANGIÇ ---
// 1. HttpClient Servisini ekle
builder.Services.AddHttpClient<JiraService>();
// --- BİZİM EKLEDİĞİMİZ KISIM BİTİŞ ---

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();