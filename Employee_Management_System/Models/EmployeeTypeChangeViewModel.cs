namespace Employee_Management_System.Models
{
    public class EmployeeTypeChangeViewModel
    {
        public int EmpCode { get; set; }

        public int Change_Type { get; set; }

        public DateTime Effect_Date { get; set; }

        public string Operator { get; set; } = "";

        public string Supervisor { get; set; } = "";

        public List<EmployeeTypeEmployeeOption> Employees { get; set; }
            = new List<EmployeeTypeEmployeeOption>();

        public List<EmployeeTypeChangeRow> Records { get; set; }
            = new List<EmployeeTypeChangeRow>();
    }

    public class EmployeeTypeEmployeeOption
    {
        public int EmpCode { get; set; }

        public string EmployeeName { get; set; } = "";

        public int? CurrentType { get; set; }
    }

    public class EmployeeTypeChangeRow
    {
        public int SrNum { get; set; }

        public DateTime Effect_Date { get; set; }

        public int EmpCode { get; set; }

        public int Previous_Type { get; set; }

        public int Change_Type { get; set; }

        public string Operator { get; set; } = "";

        public string Supervisor { get; set; } = "";

        public string EmployeeName { get; set; } = "";
    }
}