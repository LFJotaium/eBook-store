namespace ebookStore.Models;
using Microsoft.EntityFrameworkCore;
public class EbookContext :DbContext
{
    //psql -U ebookstore_user -d ebookstore to connect with db using Terminal 
    public EbookContext(DbContextOptions<EbookContext> options) : base(options) // when it runs it will look for the connection string in the appsettings.json file
    {
    }
    public DbSet<User> Users { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Price> Prices { get; set; }
    public DbSet<BorrowedBook> BorrowedBooks { get; set; }
    public DbSet<PurchasedBook> PurchasedBooks { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<WaitingListEntry> WaitingListEntries { get; set; }

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

        modelBuilder.Entity<BorrowedBook>(entity =>
        {
            entity.HasKey(bb => bb.Id);
            entity.HasOne(bb => bb.Book)
                .WithMany()
                .HasForeignKey(bb => bb.BookId);

            entity.HasOne(bb => bb.User)
                .WithMany(u => u.BorrowedBooks) // Navigation property in User
                .HasForeignKey(bb => bb.Username)
                .OnDelete(DeleteBehavior.Cascade); // Optional: Handle delete behavior
        });
// Feedback entity configuration
        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasOne(f => f.Book)
                .WithMany()
                .HasForeignKey(f => f.BookId);
            entity.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.Username);
        });
        // WaitingListEntry entity configuration
        modelBuilder.Entity<WaitingListEntry>(entity =>
        {
            entity.HasKey(wl => wl.Id);
            entity.HasOne(wl => wl.Book)
                .WithMany()
                .HasForeignKey(wl => wl.BookId);
            entity.Property(wl => wl.Username).IsRequired();
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging(false);
    }




}