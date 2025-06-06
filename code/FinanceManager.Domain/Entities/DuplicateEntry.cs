namespace FinanceManager.Domain.Entities;
public class DuplicateEntry
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public required IEnumerable<int> EntriesId { get; set; }
}
