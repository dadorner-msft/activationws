
using ActivationWs.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivationWs.Data
{
    public class ActivationDbContext : DbContext
    {
        public ActivationDbContext(DbContextOptions<ActivationDbContext> options)
        : base(options) { }

        public DbSet<ActivationRecord> ActivationRecords { get; set; }
    }
}
