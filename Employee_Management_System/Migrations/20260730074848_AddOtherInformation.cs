using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Employee_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddOtherInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AadhaarNumber",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ITSerialNumber",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PANNumber",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PFBalance",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PFNumber",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PFOpeningBalance",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PFSerialNumber",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PensionFundOpeningBalance",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavingAccountCode",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavingBranchCode",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavingGLCode",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AadhaarNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ITSerialNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PANNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PFBalance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PFNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PFOpeningBalance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PFSerialNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PensionFundOpeningBalance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SavingAccountCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SavingBranchCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SavingGLCode",
                table: "Employees");
        }
    }
}
