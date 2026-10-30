using System;
using System.ComponentModel.DataAnnotations.Schema;
namespace Employee_Management_System.Models
{
    [Table("payfoot")]
    public class PayFoot
    {
        [Column("empl_code")] public int? EmployeeCode { get; set; }
        [Column("flag")] public int? Flag { get; set; }
        [Column("allw_dedu")] public int? AllowanceDeductionCode { get; set; }
        [Column("calc_code")] public int? CalculationCode { get; set; }
        [Column("percent")] public double? Percentage { get; set; }
        [Column("date")] public DateTime? EffectiveDate { get; set; }
        [Column("cancel_date")] public DateTime? CancelDate { get; set; }
        [Column("for_times")] public int? ForTimes { get; set; }
        [Column("glc")] public int? GLC { get; set; }
        [Column("brnc")] public int? Branch { get; set; }
        [Column("slc")] public int? SLC { get; set; }
        [Column("loan_adv_amt")] public double? LoanAdvanceAmount { get; set; }
        [Column("amt")] public double? Amount { get; set; }
        [Column("Entry_Date")] public DateTime? EntryDate { get; set; }
        [Column("Opn_By")] public string? OpenedBy { get; set; }
        [Column("Opn_IP")] public string? OpenedIP { get; set; }
    }
}
