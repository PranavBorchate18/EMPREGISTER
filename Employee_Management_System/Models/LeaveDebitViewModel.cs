using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Models
{
    public class LeaveDebitViewModel
    {
        public int SelectedYear { get; set; }
        public short? LeaveCode { get; set; }

        public List<SelectListItem> LeaveTypes { get; set; } = new();
    }

    public class LeaveDebitEmployeeViewModel
    {
        public int EmployeeCode { get; set; }
        public string EmployeeName { get; set; } = "";
        public double LeaveCredit { get; set; }
        public double LeaveDebit { get; set; }
        public double LeaveBalance { get; set; }
    }
}
