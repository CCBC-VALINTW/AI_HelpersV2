namespace AiHelpers.Services;

/// <summary>
/// Detects and collapses the classic denormalised-join shape - a dimension column (customer name,
/// category, region...) repeated on every row of a one-to-many join - into a single group header
/// per distinct value, with only the genuinely per-row detail columns underneath. Pure data
/// reshaping over rows already fetched into memory: no query rewriting, no schema knowledge, no
/// per-query configuration, and no information loss - every value is still present, just written
/// once per group instead of once per row.
///
/// Deliberately a heuristic over the actual returned rows, not the query text: a column is a
/// group-column candidate if its value tends to repeat from one row to the next, which is exactly
/// what a join produces when the driving table's own row order clusters matching child rows
/// together (true for the overwhelming majority of real report-style queries). It does NOT require
/// an ORDER BY to be correct - if related rows aren't adjacent, nothing will look stable enough to
/// group and this falls back to the original flat shape untouched. (One genuine edge case: if
/// every single column is identical across every row - not a realistic query result - it still
/// groups, with one trivial column left as "detail" per the invariant below; the saving there is
/// negligible rather than the real reduction this is designed for, but it's not a regression.)
///
/// Real bug fixed 2026-09-07: an earlier version judged each column independently ("repeats more
/// than half the time?") and only bailed out to flat if literally every column qualified. That
/// broke on a real sensor-reading dataset: a slowly-drifting value column (e.g. a freezer
/// temperature, sampled often enough that it plateaus for several consecutive readings) also
/// crossed that same per-column threshold on its own, even though it's exactly the column that
/// should stay as detail - once every column "qualified" independently, the safety check fired and
/// did nothing at all, the worst possible outcome given this was actually the best-case dataset for
/// compaction (six stable columns, genuinely repetitive). Fixed by judging columns together, not
/// independently - see BuildGroupColumns below.
/// </summary>
public static class DataResultCompactor
{
    public sealed record CompactedGroup(object?[] GroupValues, List<object?[]> DetailRows);

    public sealed record CompactedResult(string[] GroupColumns, string[] DetailColumns, List<CompactedGroup> Groups);

    public static CompactedResult Compact(string[] columns, IReadOnlyList<object?[]> rows)
    {
        if (rows.Count < 2 || columns.Length < 2)
        {
            return Flat(columns, rows);
        }

        var groupColumnIndexes = BuildGroupColumns(columns.Length, rows);
        if (groupColumnIndexes.Count == 0)
        {
            return Flat(columns, rows);
        }

        var detailColumnIndexes = Enumerable.Range(0, columns.Length).Except(groupColumnIndexes).ToArray();
        var groupColumns = groupColumnIndexes.Select(i => columns[i]).ToArray();
        var detailColumns = detailColumnIndexes.Select(i => columns[i]).ToArray();

        var groups = new List<CompactedGroup>();
        object?[]? currentGroupValues = null;
        List<object?[]>? currentDetailRows = null;

        foreach (var row in rows)
        {
            var groupValues = groupColumnIndexes.Select(i => row[i]).ToArray();
            if (currentGroupValues is null || !groupValues.SequenceEqual(currentGroupValues))
            {
                currentGroupValues = groupValues;
                currentDetailRows = [];
                groups.Add(new CompactedGroup(currentGroupValues, currentDetailRows));
            }
            currentDetailRows!.Add(detailColumnIndexes.Select(i => row[i]).ToArray());
        }

        return new CompactedResult(groupColumns, detailColumns, groups);
    }

    /// <summary>
    /// Greedily grows the group key one column at a time, most-stable-first, only keeping a
    /// column if the group key *as a whole* still collapses the rows into meaningfully fewer
    /// groups than there are rows (under half). This is what independent per-column judging
    /// couldn't do: two columns can each look individually "stable enough" while their
    /// combination fragments into nearly one group per row (the sensor-reading bug above) - only
    /// checking the actual combined effect catches that. Always leaves at least one column as
    /// detail, even in the degenerate case where every column happens to move in lockstep.
    /// </summary>
    private static List<int> BuildGroupColumns(int columnCount, IReadOnlyList<object?[]> rows)
    {
        var transitionCounts = new int[columnCount];
        for (var r = 1; r < rows.Count; r++)
        {
            for (var c = 0; c < columnCount; c++)
            {
                if (!Equals(rows[r][c], rows[r - 1][c]))
                {
                    transitionCounts[c]++;
                }
            }
        }

        // Try the most stable (fewest transitions) columns first - the best group-column
        // candidates - so the greedy walk below adds the columns most likely to pay off before
        // ever trying a more volatile one.
        var candidateOrder = Enumerable.Range(0, columnCount).OrderBy(c => transitionCounts[c]).ToArray();

        var groupColumnIndexes = new List<int>();
        foreach (var candidate in candidateOrder)
        {
            if (groupColumnIndexes.Count + 1 >= columnCount)
            {
                // Never let every column become the group key - there must always be at least one
                // genuinely per-row detail column left, regardless of how stable this last
                // candidate looks in isolation.
                break;
            }

            var trialGroupCount = CountContiguousGroups(rows, [.. groupColumnIndexes, candidate]);
            if (trialGroupCount >= rows.Count / 2.0)
            {
                // This column (combined with whatever's already in the group key) no longer
                // meaningfully collapses the rows - it's behaving like per-row detail, not a
                // repeating dimension. Every remaining candidate is at least as volatile as this
                // one (candidates are tried most-stable-first), so nothing further would help.
                break;
            }

            groupColumnIndexes.Add(candidate);
        }

        groupColumnIndexes.Sort(); // restore original column order for a readable group header
        return groupColumnIndexes;
    }

    /// <summary>Number of contiguous runs the given columns, taken together, partition the rows
    /// into - i.e. how many group headers Compact would actually emit for this combination.</summary>
    private static int CountContiguousGroups(IReadOnlyList<object?[]> rows, int[] columnIndexes)
    {
        var count = 1;
        for (var r = 1; r < rows.Count; r++)
        {
            foreach (var c in columnIndexes)
            {
                if (!Equals(rows[r][c], rows[r - 1][c]))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    /// <summary>The "nothing to compact" shape - every column is detail, one group with no group
    /// columns at all. Formatters check GroupColumns.Length == 0 to render this as a plain flat
    /// table, identical to the original pre-compaction output.</summary>
    private static CompactedResult Flat(string[] columns, IReadOnlyList<object?[]> rows) =>
        new([], columns, [new CompactedGroup([], rows.ToList())]);
}
