namespace JiraEntegrasyonApi.Core.Dtos
{
    public record CreateTaskDto(
        string HataBasligi,
        string Detay,
        string Modul,
        string Oncelik
    );
}