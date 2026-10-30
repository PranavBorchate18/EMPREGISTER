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

        // From Date
        public DateTime? DemandFromDate { get; set; }


        // =========================================================
        // NUMBER OF DAYS
        //
        // User enters number of leave days.
        //
        // Example:
        // From Date      = 16-09-2026
        // Number Of Days = 6
        // To Date        = 21-09-2026
        //
        // Formula:
        // To Date = From Date + (Number Of Days - 1)
        //
        // Database:
        // dbo.leavtrn.Num_of_days
        // =========================================================

        public double? NumberOfDays { get; set; }


        // To Date - calculated automatically
        public DateTime? DemandToDate { get; set; }


        // Leave Type
        public int? DemandLeaveCode { get; set; }


        // Previous Days
        public double? PreviousDays { get; set; }


        // =========================================================
        // REASON
        //
        // Selected Reason Code from dbo.resnmast
        //
        // Example:
        // 1 = POOJA
        // 2 = SICK
        // 3 = MARRIAGE
        // 4 = PERSONAL
        // 5 = Other
        //
        // Only code is stored in dbo.leavtrn.reason
        // =========================================================

        public int? ReasonCode { get; set; }

        public string? ReasonRemark { get; set; }


        // =========================================================
        // CHARGE TO TRANSFER
        // =========================================================

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
        //
        // Source:
        // dbo.leavmast
        // =========================================================

        public List<SelectListItem> LeaveTypes { get; set; }
            = new List<SelectListItem>();


        // =========================================================
        // REASON MASTER DROPDOWN
        //
        // Source:
        // dbo.resnmast
        //
        // Value = code
        // Text  = code - name
        //
        // Example:
        // 1 - POOJA
        // 2 - SICK
        // 3 - MARRIAGE
        // 4 - PERSONAL
        // 5 - Other
        //
        // Future Reason added in dbo.resnmast will automatically
        // appear in dropdown.
        // =========================================================

        public List<SelectListItem> ReasonTypes { get; set; }
            = new List<SelectListItem>();
    }
}