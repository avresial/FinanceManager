using CsvHelper.Configuration.Attributes;

namespace FinanceManager.Models
{
    public class AccountEntry
    {
        [Index(0)]
        public DateTime PostingDate { get; set; }
        
        [Index(1)]
        public double Balance { get; set; }
        
        [Index(2)]
        public double BalanceChange { get; set; }
        
        [Index(3)]
        public string SenderName { get; set; }
    
    }
}
