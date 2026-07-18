using Microsoft.EntityFrameworkCore;
using TrovaBackend.Models;

namespace TrovaBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Models.CompanyDetails> CompanyDetails => Set<Models.CompanyDetails>();
    public DbSet<Models.BankConnection> BankConnections => Set<Models.BankConnection>();
    public DbSet<Models.CapabilityScore> CapabilityScores => Set<Models.CapabilityScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Models.CompanyDetails>(entity =>
        {
            // One company profile per user — Submit() upserts against this.
            entity.HasIndex(c => c.UserId).IsUnique();
        });

        modelBuilder.Entity<Models.BankConnection>(entity =>
        {
            entity.HasIndex(b => b.UserId).IsUnique();
        });

        modelBuilder.Entity<Models.CapabilityScore>(entity =>
        {
            entity.HasIndex(s => s.UserId).IsUnique();
        });

        // Add further entity configuration here as the domain grows.
    }
}
