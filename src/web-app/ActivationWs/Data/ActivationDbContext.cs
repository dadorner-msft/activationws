using ActivationWs.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivationWs.Data
{
    public class ActivationDbContext : DbContext
    {
        public ActivationDbContext(DbContextOptions<ActivationDbContext> options)
            : base(options) { }

        public DbSet<Machine> Machines => Set<Machine>();
        public DbSet<ActivationRecord> ActivationRecords => Set<ActivationRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Machine configuration
            modelBuilder.Entity<Machine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Hostname)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.HasIndex(e => e.Hostname)
                    .IsUnique();
            });

            // ActivationRecord configuration
            modelBuilder.Entity<ActivationRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.ExtendedProductID)
                    .IsRequired()
                    .HasMaxLength(100);
                    
                entity.Property(e => e.InstallationID)
                    .IsRequired()
                    .HasMaxLength(100);
                    
                entity.Property(e => e.ConfirmationID)
                    .IsRequired()
                    .HasMaxLength(100);

                // Enforce unique combination per machine
                entity.HasIndex(a => new { a.MachineId, a.InstallationID, a.ExtendedProductID })
                    .IsUnique()
                    .HasDatabaseName("IX_ActivationRecord_Unique");

                // Foreign key relationship
                entity.HasOne(a => a.Machine)
                    .WithMany(m => m.ActivationRecords)
                    .HasForeignKey(a => a.MachineId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Index for performance
                entity.HasIndex(a => a.LicenseAcquisitionDate)
                    .HasDatabaseName("IX_ActivationRecord_LicenseDate");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
