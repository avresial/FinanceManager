namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Packs equal-height dashboard cards into 12-column rows, keeping their order. Every way of
/// breaking the card sequence into rows is considered, and the one chosen leaves the least
/// unfilled space, then widens cards the least (so cards stay as close to their preferred width
/// as possible); among equal layouts, earlier rows are the fuller ones. Within a row, spare columns go to the cards with the most room to grow, never
/// past their <see cref="DashboardCardSpan.Max"/>; whatever is still left becomes a filler tile
/// instead of a stretched card or an empty gap.
/// </summary>
public static class DashboardCardLayout
{
    private const int _gridColumns = 12;

    public static IReadOnlyList<DashboardCardPlacement> Arrange(IReadOnlyList<DashboardCardSpan> cards)
    {
        foreach (var card in cards)
        {
            if (card.Preferred < 1 || card.Preferred > card.Max || card.Max > _gridColumns)
                throw new ArgumentException($"Invalid card span {card}.", nameof(cards));
        }

        // best[i] is the cheapest layout of cards[i..]; rowEnd[i] is where its first row ends.
        var count = cards.Count;
        var best = new RowCost[count + 1];
        var rowEnd = new int[count + 1];
        best[count] = new RowCost(0, 0);

        for (var start = count - 1; start >= 0; start--)
        {
            best[start] = RowCost.Worst;
            var preferredWidth = 0;
            for (var end = start + 1; end <= count; end++)
            {
                preferredWidth += cards[end - 1].Preferred;
                if (preferredWidth > _gridColumns)
                    break;

                // On a tie the longer first row wins, so complete rows come before short ones.
                var cost = WidenRow(cards, start, end).Cost + best[end];
                if (!(best[start] < cost))
                {
                    best[start] = cost;
                    rowEnd[start] = end;
                }
            }
        }

        var placements = new List<DashboardCardPlacement>(count);
        for (var start = 0; start < count; start = rowEnd[start])
        {
            var row = WidenRow(cards, start, rowEnd[start]);
            for (var i = 0; i < row.Spans.Length; i++)
                placements.Add(new DashboardCardPlacement(row.Spans[i], i == row.Spans.Length - 1 ? row.Cost.Filler : 0));
        }

        return placements;
    }

    // Starts every card in cards[start..end) at its preferred width and hands out the spare
    // columns one at a time to the card with the most room left to grow (earliest on ties).
    private static (int[] Spans, RowCost Cost) WidenRow(IReadOnlyList<DashboardCardSpan> cards, int start, int end)
    {
        var spans = new int[end - start];
        for (var i = 0; i < spans.Length; i++)
            spans[i] = cards[start + i].Preferred;

        var free = _gridColumns - spans.Sum();
        var widening = 0;
        while (free > 0)
        {
            var grow = -1;
            for (var i = 0; i < spans.Length; i++)
            {
                var room = cards[start + i].Max - spans[i];
                if (room > 0 && (grow < 0 || room > cards[start + grow].Max - spans[grow]))
                    grow = i;
            }

            if (grow < 0)
                break;

            spans[grow]++;
            free--;
            widening++;
        }

        return (spans, new RowCost(free, widening));
    }

    // Unfilled columns weigh more than any amount of widening, so a gap-free layout always wins.
    private readonly record struct RowCost(int Filler, int Widening)
    {
        public static RowCost Worst { get; } = new(int.MaxValue / 2, int.MaxValue / 2);

        public static RowCost operator +(RowCost left, RowCost right) =>
            new(left.Filler + right.Filler, left.Widening + right.Widening);

        public static bool operator <(RowCost left, RowCost right) =>
            left.Filler < right.Filler || (left.Filler == right.Filler && left.Widening < right.Widening);

        public static bool operator >(RowCost left, RowCost right) => right < left;
    }
}