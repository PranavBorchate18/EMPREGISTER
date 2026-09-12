using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Models
{
    public class LeaveApplicationViewModel
    {
        // =========================================================
        // EMPLOYEE
        // =========================================================

        public int EmployeeCode { get; set; }

        public string? EmployeeName { get; set; }


        // =========================================================
        // LEAVE / DEMAND
        // =========================================================

        public DateTime? DemandFromDate { get; set; }

        public DateTime? DemandToDate { get; set; }

        public int? DemandLeaveCode { get; set; }

        public double? PreviousDays { get; set; }

        public int? ReasonCode { get; set; }

        public string? ReasonRemark { get; set; }

        public int? ChargeTo { get; set; }


        // =========================================================
        // SANCTION
        // =========================================================

        public DateTime? SanctionFromDate { get; set; }

        public DateTime? SanctionToDate { get; set; }

        public int? SanctionLeaveCode { get; set; }

        public int? SanctionOfficer { get; set; }

        public DateTime? EffectPayDate { get; set; }


        // =========================================================
        // LEAVE MASTER DROPDOWN
        // =========================================================

        public List<SelectListItem> LeaveTypes { get; set; }
            = new List<SelectListItem>();
    }
}