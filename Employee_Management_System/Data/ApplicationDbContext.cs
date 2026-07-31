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

        // Employee Table
        public DbSet<Employee> Employees { get; set; }

        // Grade Table
        public DbSet<GradeFoot> Grades { get; set; }

        // Section Table
        public DbSet<Section> Sections { get; set; }

        // Branch Table
        public DbSet<Branch> Branches { get; set; }
    }
}


