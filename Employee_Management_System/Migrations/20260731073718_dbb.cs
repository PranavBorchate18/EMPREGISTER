using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Employee_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class dbb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Branches_BranchId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Grades_GradeId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Sections_SectionId",
                table: "Employees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Sections",
                table: "Sections");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Grades",
                table: "Grades");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Employees",
                table: "Employees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Branches",
                table: "Branches");

            migrationBuilder.RenameTable(
                name: "Sections",
                newName: "SectionMaster");

            migrationBuilder.RenameTable(
                name: "Grades",
                newName: "GradeMaster");

            migrationBuilder.RenameTable(
                name: "Employees",
                newName: "Employee");

            migrationBuilder.RenameTable(
                name: "Branches",
                newName: "BranchMaster");

            migrationBuilder.RenameIndex(
                name: "IX_Employees_SectionId",
                table: "Employee",
                newName: "IX_Employee_SectionId");

            migrationBuilder.RenameIndex(
                name: "IX_Employees_GradeId",
                table: "Employee",
                newName: "IX_Employee_GradeId");

            migrationBuilder.RenameIndex(
                name: "IX_Employees_BranchId",
                table: "Employee",
                newName: "IX_Employee_BranchId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SectionMaster",
                table: "SectionMaster",
                column: "code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GradeMaster",
                table: "GradeMaster",
                column: "code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Employee",
                table: "Employee",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BranchMaster",
                table: "BranchMaster",
                column: "code");

            migrationBuilder.AddForeignKey(
                name: "FK_Employee_BranchMaster_BranchId",
                table: "Employee",
                column: "BranchId",
                principalTable: "BranchMaster",
                principalColumn: "code");

            migrationBuilder.AddForeignKey(
                name: "FK_Employee_GradeMaster_GradeId",
                table: "Employee",
                column: "GradeId",
                principalTable: "GradeMaster",
                principalColumn: "code");

            migrationBuilder.AddForeignKey(
                name: "FK_Employee_SectionMaster_SectionId",
                table: "Employee",
                column: "SectionId",
                principalTable: "SectionMaster",
                principalColumn: "code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employee_BranchMaster_BranchId",
                table: "Employee");

            migrationBuilder.DropForeignKey(
                name: "FK_Employee_GradeMaster_GradeId",
                table: "Employee");

            migrationBuilder.DropForeignKey(
                name: "FK_Employee_SectionMaster_SectionId",
                table: "Employee");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SectionMaster",
                table: "SectionMaster");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GradeMaster",
                table: "GradeMaster");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Employee",
                table: "Employee");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BranchMaster",
                table: "BranchMaster");

            migrationBuilder.RenameTable(
                name: "SectionMaster",
                newName: "Sections");

            migrationBuilder.RenameTable(
                name: "GradeMaster",
                newName: "Grades");

            migrationBuilder.RenameTable(
                name: "Employee",
                newName: "Employees");

            migrationBuilder.RenameTable(
                name: "BranchMaster",
                newName: "Branches");

            migrationBuilder.RenameIndex(
                name: "IX_Employee_SectionId",
                table: "Employees",
                newName: "IX_Employees_SectionId");

            migrationBuilder.RenameIndex(
                name: "IX_Employee_GradeId",
                table: "Employees",
                newName: "IX_Employees_GradeId");

            migrationBuilder.RenameIndex(
                name: "IX_Employee_BranchId",
                table: "Employees",
                newName: "IX_Employees_BranchId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sections",
                table: "Sections",
                column: "code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Grades",
                table: "Grades",
                column: "code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Employees",
                table: "Employees",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Branches",
                table: "Branches",
                column: "code");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Branches_BranchId",
                table: "Employees",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "code");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Grades_GradeId",
                table: "Employees",
                column: "GradeId",
                principalTable: "Grades",
                principalColumn: "code");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Sections_SectionId",
                table: "Employees",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "code");
        }
    }
}
