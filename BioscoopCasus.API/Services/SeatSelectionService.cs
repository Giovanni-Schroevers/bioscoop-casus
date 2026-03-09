using System;
using System.Collections.Generic;
using System.Linq;
using BioscoopCasus.API.Entities;

namespace BioscoopCasus.API.Services;

public class SeatSelectionResult
{
    public List<(int RowNumber, int SeatNumber)> Seats { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public bool IsGroupedTogether { get; set; }
    public List<List<(int RowNumber, int SeatNumber)>> GroupedSeats { get; set; } = new();
}

public static class SeatSelectionService
{
    
    private const double RowDistanceWeight     = 2.0;
    private const double OrphanSeatPenalty     = 3.0;
    private const double FrontRowPenalty       = 4.0;
    private const double ExtremeSidePenalty    = 2.0;
    private const int    FrontRowThreshold     = 3;
    private const double ExtremeSideThreshold  = 0.80;

    public static SeatSelectionResult SelectBestSeats(
        Room room,
        int groupSize,
        HashSet<(int RowNumber, int SeatNumber)> occupiedSeats)
    {
        var result = new SeatSelectionResult();
        var rows   = room.Rows.OrderBy(r => r.RowNumber).ToList();

        if (!rows.Any())
        {
            result.Message = "No seats available in this room.";
            return result;
        }

        int    totalRows    = rows.Count;
        int    middleRow    = (totalRows + 1) / 2;
        int    maxSeatCount = rows.Max(r => r.SeatCount);
        int    middleSeat   = (maxSeatCount + 1) / 2;
        double splitPenalty = (totalRows * RowDistanceWeight + maxSeatCount) * groupSize * 4.0 + 1.0;

        var rowMeta     = BuildRowMetadata(rows, occupiedSeats, groupSize, middleRow, middleSeat, totalRows);
        var candidates  = rowMeta.Candidates;
        var lowerBounds = BuildLowerBoundTable(rowMeta.SortedSeatScores, groupSize);

        var allocation = SolveWithStrictPriority(candidates, lowerBounds, groupSize, splitPenalty);

        if (allocation is null)
        {
            result.Message = "Not enough seats available.";
            return result;
        }

        result.GroupedSeats      = allocation.Select(b => b.ToSeats()).ToList();
        result.Seats             = result.GroupedSeats.SelectMany(g => g).ToList();
        result.IsGroupedTogether = allocation.Count == 1;
        result.Message           = allocation.Count == 1
            ? "You can sit together."
            : GenerateSplitMessage(allocation.Select(b => b.Size).ToList());

        return result;
    }

    private static List<BlockCandidate>? SolveWithStrictPriority(
        List<BlockCandidate> candidates,
        double[] lowerBounds,
        int groupSize,
        double splitPenalty)
    {
        foreach (var partition in GeneratePartitions(groupSize))
        {
            var solver = new BranchBoundSolver(candidates, lowerBounds, splitPenalty, partition.Count);
            var result = solver.Solve(partition);
            if (result is not null)
                return result;
        }
        return null;
    }

    private sealed class BranchBoundSolver
    {
        private readonly List<BlockCandidate> _candidates;
        private readonly double[]             _lowerBounds;
        private readonly double               _splitPenalty;
        private readonly int                  _numGroups;

        private List<BlockCandidate>? _bestSelection;
        private double                _bestCost = double.MaxValue;

        private readonly List<BlockCandidate> _current  = new();
        private readonly HashSet<int>         _usedRows = new();

        public BranchBoundSolver(
            List<BlockCandidate> candidates,
            double[]             lowerBounds,
            double               splitPenalty,
            int                  numGroups)
        {
            _candidates   = candidates;
            _lowerBounds  = lowerBounds;
            _splitPenalty = splitPenalty;
            _numGroups    = numGroups;
        }

        public List<BlockCandidate>? Solve(List<int> partition)
        {
            var sortedPartition = partition.OrderByDescending(s => s).ToList();
            Search(0, sortedPartition, 0, 0.0);
            return _bestSelection;
        }

        private void Search(int candidateIdx, List<int> remainingSizes, int groupIdx, double costSoFar)
        {
            if (groupIdx == remainingSizes.Count)
            {
                if (costSoFar < _bestCost)
                {
                    _bestCost      = costSoFar;
                    _bestSelection = new List<BlockCandidate>(_current);
                }
                return;
            }

            int neededSize     = remainingSizes[groupIdx];
            int remainingPeople = remainingSizes.Skip(groupIdx + 1).Sum();
            double basePenalty = groupIdx > 0 ? _splitPenalty : 0.0;

            for (int i = candidateIdx; i < _candidates.Count; i++)
            {
                var block = _candidates[i];

                if (block.Size != neededSize)
                    continue;

                if (_usedRows.Contains(block.RowNumber))
                    continue;

                double newCost = costSoFar + basePenalty + block.Score;

                if (newCost >= _bestCost)
                    break;

                double lb = remainingPeople > 0 ? _lowerBounds[remainingPeople - 1] : 0.0;
                if (newCost + lb + (_numGroups - groupIdx - 1) * _splitPenalty >= _bestCost)
                    continue;

                _current.Add(block);
                _usedRows.Add(block.RowNumber);
                Search(i + 1, remainingSizes, groupIdx + 1, newCost);
                _usedRows.Remove(block.RowNumber);
                _current.RemoveAt(_current.Count - 1);
            }
        }
    }

    private static IEnumerable<List<int>> GeneratePartitions(int groupSize)
    {
        for (int numParts = 1; numParts <= groupSize; numParts++)
        {
            foreach (var p in FixedPartsPartitions(groupSize, numParts, groupSize / numParts + 1))
                yield return p;
        }
    }

    private static IEnumerable<List<int>> FixedPartsPartitions(int remaining, int partsLeft, int maxPart)
    {
        if (partsLeft == 1)
        {
            if (remaining >= 1 && remaining <= maxPart)
                yield return new List<int> { remaining };
            yield break;
        }

        for (int part = Math.Min(remaining - partsLeft + 1, maxPart); part >= (int)Math.Ceiling(remaining / (double)partsLeft); part--)
        {
            foreach (var rest in FixedPartsPartitions(remaining - part, partsLeft - 1, part))
            {
                var partition = new List<int>(rest.Count + 1) { part };
                partition.AddRange(rest);
                yield return partition;
            }
        }
    }

    private sealed record BlockCandidate(int RowNumber, int StartSeat, int Size, double Score)
    {
        public List<(int RowNumber, int SeatNumber)> ToSeats() =>
            Enumerable.Range(StartSeat, Size).Select(s => (RowNumber, s)).ToList();
    }

    private sealed record RowMetadata(List<BlockCandidate> Candidates, double[] SortedSeatScores);

    private static RowMetadata BuildRowMetadata(
        List<Row>                                    rows,
        HashSet<(int RowNumber, int SeatNumber)>     occupiedSeats,
        int                                          groupSize,
        int                                          middleRow,
        int                                          middleSeat,
        int                                          totalRows)
    {
        var candidates     = new List<BlockCandidate>();
        var allSeatScores  = new List<double>();

        foreach (var row in rows)
        {
            var available = GetAvailableSeats(row, occupiedSeats);
            if (!available.Any()) continue;

            foreach (var seat in available)
                allSeatScores.Add(RawSeatScore(row.RowNumber, seat, middleRow, middleSeat));

            var bestPerSize = new Dictionary<int, (int startSeat, double score)>();

            for (int i = 0; i < available.Count; i++)
            {
                double seatScoreSum = 0.0;

                for (int j = i; j < available.Count; j++)
                {
                    if (j > i && available[j] != available[j - 1] + 1)
                        break;

                    int size = j - i + 1;
                    if (size > groupSize) break;

                    seatScoreSum += Math.Abs(available[j] - middleSeat);

                    double blockScore = seatScoreSum
                        + Math.Abs(row.RowNumber - middleRow) * RowDistanceWeight * size
                        + OrphanPenalty(available, i, j, row.SeatCount)
                        + FrontPenalty(row.RowNumber, middleRow, size)
                        + SidePenalty(available[i], available[j], row.SeatCount, size);

                    if (!bestPerSize.TryGetValue(size, out var existing) || blockScore < existing.score)
                        bestPerSize[size] = (available[i], blockScore);
                }
            }

            foreach (var (size, (startSeat, score)) in bestPerSize)
                candidates.Add(new BlockCandidate(row.RowNumber, startSeat, size, score));
        }

        var sortedScores = allSeatScores.OrderBy(s => s).ToArray();
        return new RowMetadata(candidates.OrderBy(c => c.Score).ToList(), sortedScores);
    }

    private static double RawSeatScore(int row, int seat, int middleRow, int middleSeat) =>
        Math.Abs(row - middleRow) * RowDistanceWeight + Math.Abs(seat - middleSeat);

    private static double OrphanPenalty(List<int> available, int blockStart, int blockEnd, int totalSeats)
    {
        double penalty = 0.0;

        int leftNeighbour = available[blockStart] - 1;
        if (leftNeighbour >= 1 && !available.Contains(leftNeighbour - 1) && available.Contains(leftNeighbour))
            penalty += OrphanSeatPenalty;

        int rightNeighbour = available[blockEnd] + 1;
        if (rightNeighbour <= totalSeats && !available.Contains(rightNeighbour + 1) && available.Contains(rightNeighbour))
            penalty += OrphanSeatPenalty;

        return penalty;
    }

    private static double FrontPenalty(int rowNumber, int middleRow, int groupSize)
    {
        bool isFrontRow = rowNumber <= FrontRowThreshold || rowNumber < middleRow / 2;
        return isFrontRow ? FrontRowPenalty * groupSize : 0.0;
    }

    private static double SidePenalty(int firstSeat, int lastSeat, int totalSeats, int groupSize)
    {
        double leftRatio  = firstSeat / (double)totalSeats;
        double rightRatio = lastSeat  / (double)totalSeats;

        bool isExtremeLeft  = leftRatio  < (1.0 - ExtremeSideThreshold);
        bool isExtremeRight = rightRatio > ExtremeSideThreshold;

        return (isExtremeLeft || isExtremeRight) ? ExtremeSidePenalty * groupSize : 0.0;
    }

    private static double[] BuildLowerBoundTable(double[] sortedScores, int maxN)
    {
        int    count  = Math.Min(maxN, sortedScores.Length);
        var    table  = new double[count];
        double running = 0.0;

        for (int i = 0; i < count; i++)
        {
            running  += sortedScores[i];
            table[i]  = running;
        }
        return table;
    }

    private static List<int> GetAvailableSeats(
        Row                                          row,
        HashSet<(int RowNumber, int SeatNumber)>     occupiedSeats)
    {
        var list = new List<int>(row.SeatCount);
        for (int seat = 1; seat <= row.SeatCount; seat++)
            if (!occupiedSeats.Contains((row.RowNumber, seat)))
                list.Add(seat);
        return list;
    }

    private static string GenerateSplitMessage(List<int> groupSizes)
    {
        var descriptions = groupSizes
            .GroupBy(g => g)
            .OrderByDescending(g => g.Key)
            .Select(g => g.Count() == 1 ? $"{g.Key}" : $"{g.Key} ({g.Count()}x)");

        return $"You cannot sit all together. Best available option is groups of {string.Join(", ", descriptions)}.";
    }

    public static int CalculateSeatScore(int rowNumber, int seatNumber, int totalRows, int seatsPerRow)
    {
        int middleRow  = (totalRows  + 1) / 2;
        int middleSeat = (seatsPerRow + 1) / 2;
        return (int)(Math.Abs(rowNumber  - middleRow)  * RowDistanceWeight
                   + Math.Abs(seatNumber - middleSeat));
    }
}