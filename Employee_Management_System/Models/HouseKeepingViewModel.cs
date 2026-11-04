namespace Employee_Management_System.Models
{
    public class HouseKeepingViewModel
    {
        public int SelectedYear { get; set; }
        public int SelectedMonth { get; set; }
        // 0 = All, 1 = Permanent (PayMast/paytrn types 1-5),
        // 6 = Contract, 7 = Temporary, 9 and 10 = separate employee types.
        public int SelectedEmployeeType { get; set; }
        public List<HouseKeepingBranchViewModel> Branches { get; set; } = new();
    }

    public class HouseKeepingBranchViewModel
    {
        public int BranchCode { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public double TotalBasic { get; set; }
        public double TotalAllowance { get; set; }
        public double TotalDeduction { get; set; }
        public double TotalNetSalary { get; set; }
    }
}
