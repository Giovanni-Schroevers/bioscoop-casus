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
    public static SeatSelectionResult SelectBestSeats(Room room, int groupSize, HashSet<(int RowNumber, int SeatNumber)> occupiedSeats)
    {
        var result = new SeatSelectionResult();

        var rows = room.Rows.OrderBy(r => r.RowNumber).ToList();
        if (!rows.Any())
        {
            result.Message = "No seats available in this room.";
            return result;
        }

        int totalRows = rows.Count;
        int middleRow = (totalRows + 1) / 2;

        var togetherResult = TryFindGroupSeats(rows, groupSize, middleRow, occupiedSeats);

        if (togetherResult != null)
        {
            result.Seats = togetherResult;
            result.IsGroupedTogether = true;
            result.Message = "You can sit together.";
            result.GroupedSeats.Add(togetherResult);
            return result;
        }

        var splitResult = TrySplitGroup(rows, groupSize, middleRow, occupiedSeats);

        if (splitResult != null)
        {
            result.GroupedSeats = splitResult.Value.Groups;
            result.Seats = splitResult.Value.Groups.SelectMany(g => g).ToList();
            result.IsGroupedTogether = false;
            result.Message = splitResult.Value.Message;
            return result;
        }

        result.Message = "Not enough seats available.";
        return result;
    }

    private static List<(int RowNumber, int SeatNumber)>? TryFindGroupSeats(
        List<Row> rows,
        int groupSize,
        int middleRow,
        HashSet<(int RowNumber, int SeatNumber)> occupiedSeats)
    {
        var sortedRows = rows
            .Select(r => new { Row = r, Distance = Math.Abs(r.RowNumber - middleRow) })
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Row.RowNumber)
            .Select(x => x.Row)
            .ToList();

        foreach (var row in sortedRows)
        {
            var availableSeats = GetAvailableSeatsInRow(row, occupiedSeats);
            if (availableSeats.Count >= groupSize)
            {
                var block = FindBestSeatBlock(availableSeats, groupSize, row.SeatCount);
                if (block != null)
                {
                    return block.Select(seatNum => (row.RowNumber, seatNum)).ToList();
                }
            }
        }

        return null;
    }

    private static List<int> GetAvailableSeatsInRow(Row row, HashSet<(int RowNumber, int SeatNumber)> occupiedSeats)
    {
        var available = new List<int>();
        for (int seat = 1; seat <= row.SeatCount; seat++)
        {
            if (!occupiedSeats.Contains((row.RowNumber, seat)))
            {
                available.Add(seat);
            }
        }
        return available;
    }

    private static List<int>? FindBestSeatBlock(List<int> availableSeats, int groupSize, int totalSeatsInRow)
    {
        if (!availableSeats.Any() || availableSeats.Count < groupSize)
            return null;

        int middleSeat = (totalSeatsInRow + 1) / 2;
        int bestStartIndex = -1;
        int bestScore = int.MaxValue;

        for (int i = 0; i <= availableSeats.Count - groupSize; i++)
        {
            bool isContinuous = true;
            for (int j = 1; j < groupSize; j++)
            {
                if (availableSeats[i + j] != availableSeats[i + j - 1] + 1)
                {
                    isContinuous = false;
                    break;
                }
            }

            if (!isContinuous)
                continue;

            int firstSeat = availableSeats[i];
            int lastSeat = availableSeats[i + groupSize - 1];
            int blockMiddle = (firstSeat + lastSeat) / 2;
            int score = Math.Abs(blockMiddle - middleSeat);

            if (score < bestScore)
            {
                bestScore = score;
                bestStartIndex = i;
            }
        }

        if (bestStartIndex == -1)
            return null;

        return availableSeats.GetRange(bestStartIndex, groupSize);
    }

    private static (List<List<(int RowNumber, int SeatNumber)>> Groups, string Message)? TrySplitGroup(
        List<Row> rows,
        int groupSize,
        int middleRow,
        HashSet<(int RowNumber, int SeatNumber)> occupiedSeats)
    {
        var splits = GetPossibleSplits(groupSize);

        foreach (var split in splits)
        {
            var groups = new List<List<(int RowNumber, int SeatNumber)>>();
            var seatsInUse = new HashSet<(int RowNumber, int SeatNumber)>();
            bool canAccommodate = true;

            foreach (var subGroupSize in split.OrderByDescending(s => s))
            {
                var subGroupResult = FindBestSubGroup(rows, middleRow, subGroupSize, occupiedSeats, seatsInUse);

                if (subGroupResult == null)
                {
                    canAccommodate = false;
                    break;
                }

                groups.Add(subGroupResult);
                foreach (var seat in subGroupResult)
                {
                    seatsInUse.Add(seat);
                }
            }

            if (canAccommodate)
            {
                string message = GenerateSplitMessage(groupSize, split);
                return (groups, message);
            }
        }

        return null;
    }

    private static List<List<int>> GetPossibleSplits(int groupSize)
    {
        return groupSize switch
        {
            1 => new List<List<int>> { new() { 1 } },
            2 => new List<List<int>> { new() { 2 } },
            3 => new List<List<int>> { new() { 3 } },
            4 => new List<List<int>> { new() { 4 }, new() { 2, 2 } },
            5 => new List<List<int>> { new() { 5 }, new() { 3, 2 }, new() { 2, 2, 1 } },
            6 => new List<List<int>> { new() { 6 }, new() { 3, 3 }, new() { 2, 2, 2 } },
            7 => new List<List<int>> { new() { 7 }, new() { 4, 3 }, new() { 3, 2, 2 } },
            8 => new List<List<int>> { new() { 8 }, new() { 4, 4 }, new() { 3, 3, 2 }, new() { 2, 2, 2, 2 } },
            9 => new List<List<int>> { new() { 9 }, new() { 5, 4 }, new() { 3, 3, 3 }, new() { 3, 2, 2, 2 } },
            10 => new List<List<int>> { new() { 10 }, new() { 5, 5 }, new() { 4, 3, 3 }, new() { 2, 2, 2, 2, 2 } },
            _ => GenerateGenericSplit(groupSize)
        };
    }

    private static List<List<int>> GenerateGenericSplit(int groupSize)
    {
        var splits = new List<List<int>>();
        splits.Add(new List<int> { groupSize });

        int numTwos = groupSize / 2;
        int remainder = groupSize % 2;
        if (numTwos >= 2)
        {
            var split = new List<int>();
            for (int i = 0; i < numTwos; i++) split.Add(2);
            if (remainder > 0) split.Add(remainder);
            splits.Add(split);
        }

        int groupCount = groupSize / 2;
        if (groupCount >= 2)
        {
            int baseSize = groupSize / groupCount;
            int extra = groupSize % groupCount;
            var split = new List<int>();
            for (int i = 0; i < groupCount; i++)
            {
                split.Add(baseSize + (i < extra ? 1 : 0));
            }
            splits.Add(split);
        }

        return splits;
    }

    private static List<(int RowNumber, int SeatNumber)>? FindBestSubGroup(
        List<Row> rows,
        int middleRow,
        int subGroupSize,
        HashSet<(int RowNumber, int SeatNumber)> occupiedSeats,
        HashSet<(int RowNumber, int SeatNumber)> seatsInUse)
    {
        var sortedRows = rows
            .Select(r => new { Row = r, Distance = Math.Abs(r.RowNumber - middleRow) })
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Row.RowNumber)
            .Select(x => x.Row)
            .ToList();

        foreach (var row in sortedRows)
        {
            var availableSeats = GetAvailableSeatsInRowWithInUse(row, occupiedSeats, seatsInUse);
            if (availableSeats.Count >= subGroupSize)
            {
                var block = FindBestSeatBlock(availableSeats, subGroupSize, row.SeatCount);
                if (block != null)
                {
                    return block.Select(seatNum => (row.RowNumber, seatNum)).ToList();
                }
            }
        }

        return null;
    }

    private static List<int> GetAvailableSeatsInRowWithInUse(
        Row row,
        HashSet<(int RowNumber, int SeatNumber)> occupiedSeats,
        HashSet<(int RowNumber, int SeatNumber)> seatsInUse)
    {
        var available = new List<int>();
        for (int seat = 1; seat <= row.SeatCount; seat++)
        {
            if (!occupiedSeats.Contains((row.RowNumber, seat)) &&
                !seatsInUse.Contains((row.RowNumber, seat)))
            {
                available.Add(seat);
            }
        }
        return available;
    }

    private static string GenerateSplitMessage(int groupSize, List<int> split)
    {
        var groupDescriptions = split
            .GroupBy(g => g)
            .Select(g => g.Count() == 1 ? $"{g.Key}" : $"{g.Key} ({g.Count()}x)")
            .ToList();

        string groupText = string.Join(", ", groupDescriptions);
        return $"You cannot sit all together. Best available option is groups of {groupText}.";
    }

    public static int CalculateSeatScore(int rowNumber, int seatNumber, int totalRows, int seatsPerRow)
    {
        int middleRow = (totalRows + 1) / 2;
        int middleSeat = (seatsPerRow + 1) / 2;

        int rowDistance = Math.Abs(rowNumber - middleRow);
        int seatDistance = Math.Abs(seatNumber - middleSeat);

        return rowDistance * 2 + seatDistance;
    }
}