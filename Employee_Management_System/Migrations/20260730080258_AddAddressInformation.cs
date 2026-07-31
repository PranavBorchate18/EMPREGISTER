using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Employee_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address1",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address2",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrespondenceAddress1",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrespondenceAddress2",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherName",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentAddress1",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentAddress2",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address1",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Address2",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CorrespondenceAddress1",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CorrespondenceAddress2",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FatherName",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PermanentAddress1",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PermanentAddress2",
                table: "Employees");
        }
    }
}
