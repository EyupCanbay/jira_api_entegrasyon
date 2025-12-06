using JiraEntegrasyonApi.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraEntegrasyonApi.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<JiraTaskLog> JiraTaskLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JiraTaskLog>().HasKey(x => x.Id);
        }
    }
}