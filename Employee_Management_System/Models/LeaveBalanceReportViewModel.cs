
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace Employee_Management_System.Models
{
    public class LeaveBalanceReportViewModel
    {
        public DateTime SelectedDate { get; set; } = DateTime.Today;

        public int? SelectedLeaveCode { get; set; }

        public List<SelectListItem> LeaveTypes { get; set; }
            = new List<SelectListItem>();

        public List<LeaveBalanceReportRowViewModel> Records { get; set; }
            = new List<LeaveBalanceReportRowViewModel>();
    }

    public class LeaveBalanceReportRowViewModel
    {
        public string EmployeeCode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string LeaveType { get; set; } = "";

        public decimal LeaveLimit { get; set; }
        public decimal LeaveTaken { get; set; }
        public decimal LeaveBalance { get; set; }
    }
}
