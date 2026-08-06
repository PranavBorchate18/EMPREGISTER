using Employee_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        //=========================================
        // DbSets
        //=========================================

        public DbSet<Employee> Employees { get; set; }

        public DbSet<GradeMaster> Grades { get; set; }
        public DbSet<ReligionMaster> Religions { get; set; }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<CastMaster> Castes { get; set; }
        public DbSet<Section> Sections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //=========================================
            // Employee
            //=========================================

            modelBuilder.Entity<Employee>()
                .ToTable("Employee");

            //=========================================
            // Grade Master
            //=========================================

            modelBuilder.Entity<GradeMaster>(entity =>
            {
                entity.ToTable("gradmast");

                entity.HasKey(e => e.Code);

                entity.Property(e => e.Code)
                      .HasColumnName("code");

                entity.Property(e => e.GradeName)
                      .HasColumnName("name");

                entity.Property(e => e.MarathiName)
                      .HasColumnName("mname");

                entity.Property(e => e.NewCode)
                      .HasColumnName("New_Code")
                      .HasPrecision(18, 2);
            });

            //=========================================
            // Branch Master
            //=========================================

            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("brncmast");

                entity.HasKey(e => e.Code);

                entity.Property(e => e.Code)
                      .HasColumnName("code");

                entity.Property(e => e.BranchName)
                      .HasColumnName("name");
            });

            //=========================================
            // Section Master
            //=========================================

            modelBuilder.Entity<Section>(entity =>
            {
                entity.ToTable("sectmast");

                entity.HasKey(e => e.Code);

                entity.Property(e => e.Code)
                      .HasColumnName("code");

                entity.Property(e => e.SectionName)
                      .HasColumnName("name");
            });

            modelBuilder.Entity<CastMaster>(entity =>
            {
                entity.ToTable("CastMast");

                entity.HasKey(e => e.Code);

                entity.Property(e => e.Code)
                      .HasColumnName("Code")
                      .HasPrecision(18, 0);

                entity.Property(e => e.CastName)
                      .HasColumnName("Name");

                entity.Property(e => e.MarathiName)
                      .HasColumnName("MName");
            });

            //=========================================
            // Religion Master
            //=========================================

            modelBuilder.Entity<ReligionMaster>(entity =>
            {
                entity.ToTable("ReligionMast");

                entity.HasKey(e => e.Code);

                entity.Property(e => e.Code)
                      .HasColumnName("Code")
                      .HasPrecision(18, 0);

                entity.Property(e => e.ReligionName)
                      .HasColumnName("Name");

                entity.Property(e => e.MarathiName)
                      .HasColumnName("MName");
            });

            //=========================================
            // Relationships
            //=========================================

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Grade)
                .WithMany(g => g.Employees)
                .HasForeignKey(e => e.GradeId)
                .HasPrincipalKey(g => g.Code)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Section)
                .WithMany(s => s.Employees)
                .HasForeignKey(e => e.SectionId)
                .HasPrincipalKey(s => s.Code)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Branch)
                .WithMany(b => b.Employees)
                .HasForeignKey(e => e.BranchId)
                .HasPrincipalKey(b => b.Code)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}