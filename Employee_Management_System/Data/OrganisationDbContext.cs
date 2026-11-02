using Employee_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Data
{
    public class OrganisationDbContext : DbContext
    {
        public OrganisationDbContext(
            DbContextOptions<OrganisationDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrganisationMaster> OrganisationMasters
        {
            get;
            set;
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<OrganisationMaster>(entity =>
            {
                entity.ToTable("OrganisationMaster", "dbo");

                entity.HasKey(x => x.Code);

                entity.Property(x => x.Code)
                    .HasColumnName("Code")
                    .HasMaxLength(20)
                    .IsRequired()
                    .ValueGeneratedNever();

                entity.Property(x => x.DatabaseName)
                    .HasColumnName("DatabaseName")
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(x => x.OrganisationName)
                    .HasColumnName("OrganisationName")
                    .HasMaxLength(200)
                    .IsRequired();
            });
        }
    }
}