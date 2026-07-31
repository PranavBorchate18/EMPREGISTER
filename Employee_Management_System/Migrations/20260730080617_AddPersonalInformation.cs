using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Employee_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodGroup",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Caste",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Education",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Height",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationMark",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KnownLanguage",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeOfSign",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherTongue",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BloodGroup",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Caste",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Education",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IdentificationMark",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "KnownLanguage",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ModeOfSign",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MotherTongue",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "Employees");
        }
    }
}
