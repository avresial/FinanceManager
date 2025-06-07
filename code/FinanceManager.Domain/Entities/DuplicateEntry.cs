namespace FinanceManager.Domain.Entities;
public class DuplicateEntry
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public required List<int> EntriesId { get; set; }
}
