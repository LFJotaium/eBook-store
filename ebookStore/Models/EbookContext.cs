namespace ebookStore.Models;
using Microsoft.EntityFrameworkCore;
public class EbookContext :DbContext
{
    public EbookContext(DbContextOptions<EbookContext> options) : base(options) // when it runs it will look for the connection string in the appsettings.json file
    {
    }
    public DbSet<User> Users { get; set; }
    public DbSet<Book> Books { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Username);
            entity.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(50);
            entity.Property(u => u.Email).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique(); // Unique email
            entity.Property(u => u.Role).HasDefaultValue("User");
        });
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging(false);
    }


}