namespace FinanceManager.Domain.Entities.Users;

public class RecordCapacity
{
    public int UsedCapacity { get; set; }
    public int TotalCapacity { get; set; }

    public double GetStorageUsedPercentage() => TotalCapacity > 0 ? (double)UsedCapacity / TotalCapacity * 100 : 0;
}