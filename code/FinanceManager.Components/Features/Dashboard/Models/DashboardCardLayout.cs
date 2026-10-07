namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Packs a run of same-height dashboard cards into 12-column rows, keeping their order.
/// Cards are spread evenly across the fewest rows that fit them at their preferred width
/// (so 4 third-width cards become 2 + 2, not 3 + 1). Each row then widens its cards uniformly,
/// but only as far as every card's <see cref="DashboardCardSpan.Max"/> allows; whatever width is
/// still left becomes a filler tile instead of a stretched card or an empty gap.
/// <see cref="FillingOrder"/> can first move one card so that a card allowed to widen ends up
/// in the short row, avoiding the filler where the cards' widths make that possible.
/// </summary>
public static class DashboardCardLayout
{
    private const int _gridColumns = 12;

    // Uniform card widths a row may use, widest first; each divides the grid evenly.
    private static readonly int[] _rowSpans = [12, 6, 4, 3, 2, 1];

    public static IReadOnlyList<DashboardCardPlacement> Arrange(IReadOnlyList<DashboardCardSpan> cards)
    {
        if (cards.Count == 0)
            return [];

        foreach (var card in cards)
        {
            if (card.Preferred < 1 || card.Preferred > card.Max || card.Max > _gridColumns)
                throw new ArgumentException($"Invalid card span {card}.", nameof(cards));
        }

        var perRow = _gridColumns / cards.Max(card => card.Preferred);
        var rowCount = (cards.Count + perRow - 1) / perRow;
        var cardsPerRow = cards.Count / rowCount;
        var rowsWithExtraCard = cards.Count % rowCount;

        var placements = new List<DashboardCardPlacement>(cards.Count);
        var start = 0;
        for (var row = 0; row < rowCount; row++)
        {
            var rowCards = cards.Skip(start).Take(cardsPerRow + (row < rowsWithExtraCard ? 1 : 0)).ToList();
            var span = RowSpan(rowCards);
            var filler = _gridColumns - span * rowCards.Count;

            for (var i = 0; i < rowCards.Count; i++)
                placements.Add(new DashboardCardPlacement(span, i == rowCards.Count - 1 ? filler : 0));

            start += rowCards.Count;
        }

        return placements;
    }

    /// <summary>
    /// The order to lay <paramref name="cards"/> out in: unchanged when it already leaves no
    /// filler, otherwise the arrangement with one card moved to the end that leaves the least
    /// filler. Cards nearer the end are tried first so the order changes as little as possible.
    /// </summary>
    public static IReadOnlyList<int> FillingOrder(IReadOnlyList<DashboardCardSpan> cards)
    {
        var original = Enumerable.Range(0, cards.Count).ToList();
        var best = original;
        var bestFiller = FillerFor(cards, original);

        for (var moved = cards.Count - 2; moved >= 0 && bestFiller > 0; moved--)
        {
            var candidate = original.Where(i => i != moved).Append(moved).ToList();
            var filler = FillerFor(cards, candidate);
            if (filler < bestFiller)
            {
                best = candidate;
                bestFiller = filler;
            }
        }

        return best;
    }

    private static int FillerFor(IReadOnlyList<DashboardCardSpan> cards, IReadOnlyList<int> order) =>
        Arrange([.. order.Select(i => cards[i])]).Sum(placement => placement.FillerSpanAfter);

    // The widest uniform width that fits the row, is no narrower than any card prefers
    // and no wider than any card allows.
    private static int RowSpan(IReadOnlyList<DashboardCardSpan> rowCards)
    {
        var minimum = rowCards.Max(card => card.Preferred);
        var maximum = Math.Min(rowCards.Min(card => card.Max), _gridColumns / rowCards.Count);
        return _rowSpans.FirstOrDefault(span => span >= minimum && span <= maximum, minimum);
    }
}