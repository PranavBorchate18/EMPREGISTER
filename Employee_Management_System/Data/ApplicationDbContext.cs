using Employee_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Data
{
    public partial class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }

        public DbSet<GradeFoot> Grades { get; set; }

        public DbSet<Section> Sections { get; set; }

        public DbSet<Branch> Branches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<GradeFoot>().ToTable("Grades");

            modelBuilder.Entity<Branch>().ToTable("Branches");

            modelBuilder.Entity<Section>().ToTable("Sections");
        }
    }
}