using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Models
{
    public class LeaveCreditViewModel
    {
        public int? SelectedYear { get; set; }

        public short? LeaveCode { get; set; }

        public double? NumberOfDays { get; set; }

        public List<int> SelectedEmployeeCodes { get; set; }
            = new List<int>();

        public List<SelectListItem> LeaveTypes { get; set; }
            = new List<SelectListItem>();

        public List<LeaveCreditBranchViewModel> Branches { get; set; }
            = new List<LeaveCreditBranchViewModel>();
    }

    public class LeaveCreditBranchViewModel
    {
        public int BranchCode { get; set; }

        public string? BranchName { get; set; }

        public int EmployeeCount { get; set; }

        public int SelectedEmployeeCount { get; set; }
    }

    public class LeaveCreditEmployeeViewModel
    {
        public int EmployeeCode { get; set; }

        public string? EmployeeName { get; set; }

        public bool IsSelected { get; set; }
    }
}