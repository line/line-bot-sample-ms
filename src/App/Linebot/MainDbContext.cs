using App.Linebot.Conversations.Core;
using App.Linebot.Persons;
using Microsoft.EntityFrameworkCore;

namespace App.Linebot
{
    /// <summary>
    /// メインデータベース
    /// </summary>
    public class MainDbContext : DbContext
    {
        public MainDbContext(DbContextOptions<MainDbContext> options)
            : base(options)
        {
        }

        public DbSet<Conversation> Conversation { get; private set; }

        public DbSet<PersonGroup> PersonGroup { get; private set; }

        public DbSet<Person> Person { get; private set; }
    }
}
