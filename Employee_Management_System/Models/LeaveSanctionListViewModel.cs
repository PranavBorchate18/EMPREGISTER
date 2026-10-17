using System;
namespace Employee_Management_System.Models
{
    public class LeaveSanctionListViewModel
    {
        public decimal? LeaveApplicationId { get; set; }
        public int EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public short? CDCode { get; set; }
        public DateTime? DemandFromDate { get; set; }
        public DateTime? DemandToDate { get; set; }
        public int? DemandLeaveCode { get; set; }
        public string? DemandLeaveName { get; set; }
        public double? PreviousDays { get; set; }
        public int? ReasonCode { get; set; }
        public string? ReasonRemark { get; set; }
        public double? NumberOfDays { get; set; }
        public int? ChargeTo { get; set; }
        public string? ChargeToEmployeeName { get; set; }
        public DateTime? SanctionFromDate { get; set; }
        public DateTime? SanctionToDate { get; set; }
        public int? SanctionLeaveCode { get; set; }
        public string? SanctionLeaveName { get; set; }
        public int? SanctionOfficer { get; set; }
        public DateTime? EffectPayDate { get; set; }
        public string? Operator { get; set; }
        // P = Pending, A = Approved, R = Rejected
        public string? Status { get; set; }
    }
}
