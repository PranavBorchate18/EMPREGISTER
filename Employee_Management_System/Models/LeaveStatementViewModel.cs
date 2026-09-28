namespace Employee_Management_System.Models
{
    public class LeaveStatementViewModel
    {
        // SEARCH
        public int? Year { get; set; }
        public int? EmployeeCode { get; set; }
        public DateTime? SanctionFrom { get; set; }
        public DateTime? SanctionTo { get; set; }

        // EMPLOYEE DETAILS
        public string? EmployeeName { get; set; }

        // dbo.PayMast.join_date
        public DateTime? JoiningDate { get; set; }

        // STATEMENT SUMMARY
        public double TotalCredit { get; set; }
        public double TotalDebit { get; set; }
        public double LeaveBalance { get; set; }

        // OPENING BALANCE OF SELECTED PERIOD
        public double OpeningBalance { get; set; }

        // PAGE STATE
        public bool IsSearched { get; set; }
        public string? ErrorMessage { get; set; }

        // TRANSACTION DETAILS
        public List<LeaveStatementTransactionViewModel> Transactions { get; set; }
            = new List<LeaveStatementTransactionViewModel>();
    }

    public class LeaveStatementTransactionViewModel
    {
        // DATABASE VALUES
        public DateTime SanctionFrom { get; set; }
        public DateTime? SanctionTo { get; set; }
        public short? CreditDebitCode { get; set; }
        public int SanctionCode { get; set; }
        public DateTime? DemandFrom { get; set; }
        public DateTime? DemandTo { get; set; }
        public int? DemandCode { get; set; }
        public double? PreviousDays { get; set; }
        public double? NumberOfDays { get; set; }
        public int? SanctionOfficer { get; set; }
        public int? Reason { get; set; }
        public string? ReasonRemark { get; set; }
        public int? ChargeTo { get; set; }
        public DateTime? EffectPayDate { get; set; }
        public decimal? LeaveAppId { get; set; }
        public string? Operator { get; set; }
        public string? Status { get; set; }

        // CALCULATED VALUES
        public double OpeningBalance { get; set; }
        public double CreditDays { get; set; }
        public double DebitDays { get; set; }
        public double BalanceDays { get; set; }

        public double ClosingBalance
        {
            get { return BalanceDays; }
        }
    }
}
