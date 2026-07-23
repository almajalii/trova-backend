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
    public DbSet<Models.Project> Projects => Set<Models.Project>();
    public DbSet<Models.Bid> Bids => Set<Models.Bid>();
    public DbSet<Models.GuaranteeApplication> GuaranteeApplications => Set<Models.GuaranteeApplication>();
    public DbSet<Models.GuaranteeDocument> GuaranteeDocuments => Set<Models.GuaranteeDocument>();
    public DbSet<Models.Review> Reviews => Set<Models.Review>();
    public DbSet<Models.Notification> Notifications => Set<Models.Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();

            // Admin's pending-users queue filters on this.
            entity.HasIndex(u => u.ApprovalStatus);
        });

        modelBuilder.Entity<Models.CompanyDetails>(entity =>
        {
            // One company profile per user — Submit() upserts against this.
            entity.HasIndex(c => c.UserId).IsUnique();

            // Nullable + unique — Postgres treats multiple NULLs as
            // distinct, so companies that haven't had a code generated
            // yet (PublicCode still null) don't collide with each other.
            entity.HasIndex(c => c.PublicCode).IsUnique();
        });

        modelBuilder.Entity<Models.BankConnection>(entity =>
        {
            entity.HasIndex(b => b.UserId).IsUnique();
        });

        modelBuilder.Entity<Models.CapabilityScore>(entity =>
        {
            entity.HasIndex(s => s.UserId).IsUnique();
        });

        modelBuilder.Entity<Models.Project>(entity =>
        {
            // Public-facing identifier — must be unique, this is how
            // owners/contractors reference a project everywhere else.
            entity.HasIndex(p => p.ProjectCode).IsUnique();

            // Not unique — an owner posts many projects. Indexed because
            // "My Projects" / "Project History" will filter by this.
            entity.HasIndex(p => p.OwnerId);
        });

        modelBuilder.Entity<Models.Bid>(entity =>
        {
            // Bid count per project (My Projects list) and project detail
            // lookups both filter by this.
            entity.HasIndex(b => b.ProjectId);

            // A contractor can only have one bid on a given project.
            entity.HasIndex(b => new { b.ProjectId, b.ContractorId }).IsUnique();
        });

        modelBuilder.Entity<Models.GuaranteeApplication>(entity =>
        {
            entity.HasIndex(g => g.ApplicationCode).IsUnique();
            entity.HasIndex(g => g.ContractorId);
            entity.HasIndex(g => g.ProjectId);
        });

        modelBuilder.Entity<Models.GuaranteeDocument>(entity =>
        {
            entity.HasIndex(d => d.GuaranteeApplicationId);
        });

        modelBuilder.Entity<Models.Review>(entity =>
        {
            // One review per project — see the comment on Review.ProjectId.
            entity.HasIndex(r => r.ProjectId).IsUnique();
            entity.HasIndex(r => r.RevieweeId);
        });

        modelBuilder.Entity<Models.Notification>(entity =>
        {
            // The feed query filters by user then sorts by recency.
            entity.HasIndex(n => new { n.UserId, n.CreatedAt });
        });

        // Add further entity configuration here as the domain grows.
    }
}