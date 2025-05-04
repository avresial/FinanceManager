namespace FinanceManager.Domain.Entities.Login;
public class RecordCapacity
{
    public int UsedCapacity { get; set; }
    public int TotalCapacity { get; set; }

    public double GetStorageUsedPercentage() =>
         (double)UsedCapacity / TotalCapacity * 100;
}
