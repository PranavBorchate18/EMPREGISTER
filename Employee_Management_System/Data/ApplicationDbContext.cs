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

        public DbSet<PartyMaster> PartyMasters { get; set; }

        public DbSet<AllowanceMaster> AllowanceMasters { get; set; }

        public DbSet<DeductionMaster> DeductionMasters { get; set; }
        public DbSet<PayMast> PayMasts { get; set; }
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
            // Party Master / Customer
            //=========================================

            modelBuilder.Entity<PartyMaster>(entity =>
            {
                entity.ToTable("prtymast");

                entity.HasKey(e => e.Code);

                entity.Property(e => e.Code)
                    .HasColumnName("CODE")
                    .HasPrecision(18, 0);

                entity.Property(e => e.Name)
                    .HasColumnName("name");

                entity.Property(e => e.EnglishName)
                    .HasColumnName("ename");

                entity.Property(e => e.Address1)
                    .HasColumnName("ADDR1");

                entity.Property(e => e.Address2)
                    .HasColumnName("ADDR2");

                entity.Property(e => e.Address3)
                    .HasColumnName("ADDR3");

                entity.Property(e => e.Address4)
                    .HasColumnName("ADDR4");

                entity.Property(e => e.Address5)
                    .HasColumnName("ADDR5");

                entity.Property(e => e.Pin)
                    .HasColumnName("PIN");

                entity.Property(e => e.CorrespondenceAddress1)
                    .HasColumnName("CorAddr1");

                entity.Property(e => e.CorrespondenceAddress2)
                    .HasColumnName("CorAddr2");

                entity.Property(e => e.CorrespondenceAddress3)
                    .HasColumnName("CorAddr3");

                entity.Property(e => e.CorrespondencePinCode)
                    .HasColumnName("CorPincCode");

                entity.Property(e => e.Phone)
                    .HasColumnName("PHONE");

                entity.Property(e => e.Phone1)
                    .HasColumnName("PHONE1");

                entity.Property(e => e.Mobile)
                    .HasColumnName("Mobile");

                entity.Property(e => e.EmailId)
                    .HasColumnName("EMAIL_ID");

                entity.Property(e => e.Sex)
                    .HasColumnName("SEX");

                entity.Property(e => e.BirthDate)
                    .HasColumnName("birthdate");

                entity.Property(e => e.FatherName)
                    .HasColumnName("FATHERNAME");

                entity.Property(e => e.PanNumber)
                    .HasColumnName("pan_no");

                entity.Property(e => e.AadhaarNumber)
                    .HasColumnName("AdharNo");

                entity.Property(e => e.Nationality)
                    .HasColumnName("NATIONALITY");

                entity.Property(e => e.Religion)
                    .HasColumnName("Religion");

                entity.Property(e => e.Caste)
                    .HasColumnName("Cast");

                entity.Property(e => e.Area)
                    .HasColumnName("Area");

                entity.Property(e => e.City)
                    .HasColumnName("City");

                entity.Property(e => e.Taluka)
                    .HasColumnName("Taluka");

                entity.Property(e => e.District)
                    .HasColumnName("District");

                entity.Property(e => e.State)
                    .HasColumnName("State");

                entity.Property(e => e.VoterIdNumber)
                    .HasColumnName("VOTERIDNO");

                entity.Property(e => e.PassportNumber)
                    .HasColumnName("PASSPORTNO");

                entity.Property(e => e.GstNumber)
                    .HasColumnName("GST_No");

                entity.Property(e => e.DrivingLicense)
                    .HasColumnName("Driving_License");

                entity.Property(e => e.DrivingLicenseExpiryDate)
                    .HasColumnName("Driving_License_ExpDate");
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