namespace FinanceManager.Domain.TransactionRules.Models;

/// <summary>
/// The mutable facts a transaction rule can match against and transform.
///
/// This model deliberately contains no transaction or import identity (no entry id,
/// account entry reference, or import row id): the rule engine operates on a copy of
/// these facts and only ever reports the resulting values, so identity is left to the
/// caller (e.g. the <c>CurrencyAccountEntry</c> being imported or edited).
/// </summary>
public sealed record TransactionFacts
{
    /// <summary>Creates facts with the given values; <paramref name="amount"/> must be non-negative.</summary>
    public TransactionFacts(
        string contractor,
        string description,
        int accountId,
        decimal amount,
        TransactionDirection direction,
        IReadOnlyList<string> labels)
    {
        if (amount < 0m)
            throw new ArgumentException("Amount must be non-negative; sign semantics is carried by Direction.", nameof(amount));

        Contractor = contractor.Trim();
        Description = description.Trim();
        AccountId = accountId;
        Amount = amount;
        Direction = direction;
        Labels = labels.Where(label => !string.IsNullOrWhiteSpace(label)).ToArray();
    }

    /// <summary>The user-visible contractor/merchant as currently stored (never null; may be empty).</summary>
    public string Contractor { get; set; }

    /// <summary>The user-visible description as currently stored (never null; may be empty).</summary>
    public string Description { get; set; }

    /// <summary>The account the transaction belongs to. Referenced for matching only; rules cannot move money between accounts.</summary>
    public int AccountId { get; set; }

    /// <summary>The transaction amount as a non-negative magnitude; <see cref="Direction"/> carries the sign.</summary>
    public decimal Amount { get; set; }

    /// <summary>Whether the amount flows in (income), out (expense), or between the user's own accounts (transfer).</summary>
    public TransactionDirection Direction { get; set; }

    /// <summary>The labels currently attached to the transaction, in order.</summary>
    public IReadOnlyList<string> Labels { get; set; }

    /// <summary>An empty set of facts, used as the JSON deserialization placeholder.</summary>
    public static TransactionFacts Empty => new(string.Empty, string.Empty, 0, 0m, TransactionDirection.Expense, []);

    /// <summary>
    /// Compares facts value-by-value, including label content and order, so two facts
    /// holding equal labels in lists of different identity compare equal.
    /// </summary>
    public bool Equals(TransactionFacts? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return Contractor == other.Contractor
            && Description == other.Description
            && AccountId == other.AccountId
            && Amount == other.Amount
            && Direction == other.Direction
            && Labels.SequenceEqual(other.Labels);
    }

    public override int GetHashCode()
    {
        var hashBuilder = new HashCode();
        hashBuilder.Add(Contractor);
        hashBuilder.Add(Description);
        hashBuilder.Add(AccountId);
        hashBuilder.Add(Amount);
        hashBuilder.Add(Direction);
        foreach (string label in Labels)
            hashBuilder.Add(label);
        return hashBuilder.ToHashCode();
    }
}