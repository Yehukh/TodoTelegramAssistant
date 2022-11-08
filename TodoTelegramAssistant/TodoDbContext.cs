using Microsoft.EntityFrameworkCore;

namespace TodoTelegramAssistant
{
    public class TodoDbContext : DbContext
    {
        public DbSet<Todo> Todos { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=.\SQLEXPRESS;Database=TodosDb;Trusted_Connection=True;");
        }
    }
}
