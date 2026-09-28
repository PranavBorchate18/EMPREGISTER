namespace Employee_Management_System.Models
{
    public class LeaveSanctionListViewModel
    {
        // =========================================================
        // LEAVE APPLICATION
        // =========================================================

        public decimal? LeaveApplicationId { get; set; }


        // =========================================================
        // EMPLOYEE
        // =========================================================

        public int EmployeeCode { get; set; }

        public string? EmployeeName { get; set; }


        // =========================================================
        // C/D CODE
        //
        // IMPORTANT:
        // Database मध्ये ठेवायचा.
        // Dashboard वर show करायचा नाही.
        // Save करताना Controller मधून always 2 store होईल.
        // =========================================================

        public short? CDCode { get; set; }


        // =========================================================
        // DEMAND / LEAVE
        // =========================================================

        public DateTime? DemandFromDate { get; set; }

        public DateTime? DemandToDate { get; set; }

        public int? DemandLeaveCode { get; set; }

        public string? DemandLeaveName { get; set; }

        public double? PreviousDays { get; set; }

        public int? ReasonCode { get; set; }

        public string? ReasonRemark { get; set; }


        // =========================================================
        // NUMBER OF DAYS
        //
        // Database Column:
        // dbo.leavtrn.Num_of_days
        //
        // Flow:
        //
        // 1. Leave Application save:
        //    Employee ने enter केलेले Days save होतील.
        //
        // 2. ViewLeave:
        //    Existing Num_of_days value Admin ला Days मध्ये दिसेल.
        //
        // 3. Admin Days edit करेल:
        //    Sanction To automatic calculate होईल.
        //
        // 4. Approve:
        //    Admin ने दिलेली final Days value
        //    dbo.leavtrn.Num_of_days मध्ये update होईल.
        // =========================================================

        public double? NumberOfDays { get; set; }


        // =========================================================
        // CHARGE TO TRANSFER
        // =========================================================

        // dbo.leavtrn.cherge_to मध्ये Employee Code
        public int? ChargeTo { get; set; }

        // फक्त dashboard display साठी Employee Name
        // dbo.PayMast मधून मिळेल
        public string? ChargeToEmployeeName { get; set; }


        // =========================================================
        // SANCTION
        //
        // Admin can edit Sanction From and Days before Approve.
        //
        // Sanction To calculation:
        //
        // Sanction To =
        // Sanction From + (NumberOfDays - 1)
        //
        // Example:
        //
        // Sanction From = 21-09-2026
        // Days          = 3
        // Sanction To   = 23-09-2026
        // =========================================================

        public DateTime? SanctionFromDate { get; set; }

        public DateTime? SanctionToDate { get; set; }

        public int? SanctionLeaveCode { get; set; }

        public string? SanctionLeaveName { get; set; }

        public int? SanctionOfficer { get; set; }

        public DateTime? EffectPayDate { get; set; }


        // =========================================================
        // OPERATOR
        // =========================================================

        public string? Operator { get; set; }


        // =========================================================
        // STATUS
        // =========================================================

        public string? Status { get; set; }
    }
}