using ActivationWs.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivationWs.Data
{
    public class ActivationDbContext : DbContext
    {
        public ActivationDbContext(DbContextOptions<ActivationDbContext> options)
            : base(options) { }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<ActivationRecord> ActivationRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Enforce unique product per machine
            modelBuilder.Entity<ActivationRecord>()
                .HasIndex(a => new { a.MachineId, a.InstallationID, a.ExtendedProductID })
                .IsUnique();

            modelBuilder.Entity<ActivationRecord>()
                .HasOne(a => a.Machine)
                .WithMany(m => m.ActivationRecords)
                .HasForeignKey(a => a.MachineId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
