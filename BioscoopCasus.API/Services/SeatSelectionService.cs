using System;
using System.Collections.Generic;
using System.Linq;
using BioscoopCasus.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Services;

public class SeatSelectionResult
{
    public List<Seat> SelectedSeats { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public bool IsGroupedTogether { get; set; }
    public List<List<Seat>> GroupedSeats { get; set; } = new();
    public double TotalScore { get; set; }
}
public static class SeatSelectionService
{
    private const double RowDistanceWeight = 2.0;
    private const double SeatDistanceWeight = 1.0;
    private const double OrphanSeatPenalty = 3.0;
    private const double FrontRowPenalty = 4.0;
    private const double ExtremeSidePenalty = 2.0;
    private const int FrontRowThreshold = 3;
    private const double ExtremeSideThreshold = 0.80;
    private const double SplitGroupPenalty = 5.0;
    
    public static SeatSelectionResult SelectBestSeats(
        Showtime showtime,
        int groupSize,
        IQueryable<ShowtimeSeat> showtimeSeats)
    {
        var result = new SeatSelectionResult();

        var occupiedSeatIds = showtimeSeats
            .Where(ss => ss.ReservationId.HasValue)
            .Select(ss => ss.SeatId)
            .ToHashSet();

        var availableSeats = showtime.Room.Seats
            .Where(s => !occupiedSeatIds.Contains(s.Id))
            .OrderBy(s => s.Row)
            .ThenBy(s => s.SeatNumber)
            .ToList();

        if (availableSeats.Count < groupSize)
        {
            result.Message = $"Not enough seats available. Required: {groupSize}, Available: {availableSeats.Count}";
            return result;
        }

        int totalRows = showtime.Room.Rows.Count;
        int maxSeatsPerRow = showtime.Room.Rows.Max(r => r.SeatCount);
        int middleRow = (totalRows + 1) / 2;
        int middleSeat = (maxSeatsPerRow + 1) / 2;

        var allocation = FindOptimalAllocation(availableSeats, groupSize, totalRows, middleRow, maxSeatsPerRow, middleSeat);

        if (allocation == null || allocation.Count == 0)
        {
            result.Message = "Could not find suitable seat configuration.";
            return result;
        }

        result.GroupedSeats = allocation;
        result.SelectedSeats = allocation.SelectMany(g => g).ToList();
        result.IsGroupedTogether = allocation.Count == 1;
        result.TotalScore = CalculateGroupScore(allocation, totalRows, middleRow, maxSeatsPerRow, middleSeat);
        result.Message = allocation.Count == 1
            ? "Great! You can sit together."
            : $"Best option: Split into {allocation.Count} group(s) - {string.Join(", ", allocation.Select(g => g.Count))} seats";

        return result;
    }
    
    private static List<List<Seat>>? FindOptimalAllocation(
        List<Seat> availableSeats,
        int groupSize,
        int totalRows,
        int middleRow,
        int maxSeatsPerRow,
        int middleSeat)
    {
        var seatsByRow = availableSeats.GroupBy(s => s.Row).ToList();

        var singleBlockAllocation = FindContiguousBlock(availableSeats, groupSize, totalRows, middleRow, maxSeatsPerRow, middleSeat);
        if (singleBlockAllocation != null)
            return new List<List<Seat>> { singleBlockAllocation };

        var splitAllocation = FindOptimalSplit(availableSeats, groupSize, totalRows, middleRow, maxSeatsPerRow, middleSeat);
        return splitAllocation ?? new List<List<Seat>>();
    }

    private static List<Seat>? FindContiguousBlock(
        List<Seat> availableSeats,
        int groupSize,
        int totalRows,
        int middleRow,
        int maxSeatsPerRow,
        int middleSeat)
    {
        var seatsByRow = availableSeats.GroupBy(s => s.Row).ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeatNumber).ToList());

        SeatBlock? bestBlock = null;
        double bestScore = double.MaxValue;

        foreach (var rowGroup in seatsByRow)
        {
            int row = rowGroup.Key;
            var rowSeats = rowGroup.Value;

            for (int i = 0; i <= rowSeats.Count - groupSize; i++)
            {
                bool isContiguous = true;
                for (int j = i; j < i + groupSize - 1; j++)
                {
                    if (rowSeats[j + 1].SeatNumber != rowSeats[j].SeatNumber + 1)
                    {
                        isContiguous = false;
                        break;
                    }
                }

                if (!isContiguous) continue;

                var block = new List<Seat>();
                for (int j = i; j < i + groupSize; j++)
                    block.Add(rowSeats[j]);

                double score = CalculateBlockScore(block, totalRows, middleRow, maxSeatsPerRow, middleSeat, isSplit: false);
                
                if (score < bestScore)
                {
                    bestScore = score;
                    bestBlock = new SeatBlock { Seats = block, Score = score };
                }
            }
        }

        return bestBlock?.Seats;
    }
    
    private static List<List<Seat>>? FindOptimalSplit(
        List<Seat> availableSeats,
        int groupSize,
        int totalRows,
        int middleRow,
        int maxSeatsPerRow,
        int middleSeat)
    {
        var partitions = GeneratePartitions(groupSize);
        var seatsByRow = availableSeats.GroupBy(s => s.Row).ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeatNumber).ToList());

        List<List<Seat>>? bestAllocation = null;
        double bestTotalScore = double.MaxValue;

        foreach (var partition in partitions)
        {
            var allocation = new List<List<Seat>>();
            var usedSeats = new HashSet<int>();
            var sortedPartition = partition.OrderByDescending(p => p).ToList();
            double totalScore = 0;
            bool valid = true;

            foreach (int size in sortedPartition)
            {
                var block = FindBestBlockOfSize(seatsByRow, size, usedSeats, totalRows, middleRow, maxSeatsPerRow, middleSeat);
                
                if (block == null)
                {
                    valid = false;
                    break;
                }

                allocation.Add(block);
                foreach (var seat in block)
                    usedSeats.Add(seat.Id);

                totalScore += CalculateBlockScore(block, totalRows, middleRow, maxSeatsPerRow, middleSeat, isSplit: true);
            }

            if (!valid) continue;

            totalScore += SplitGroupPenalty * (partition.Count - 1);

            if (totalScore < bestTotalScore)
            {
                bestTotalScore = totalScore;
                bestAllocation = allocation;
            }
        }

        return bestAllocation;
    }

    private static List<Seat>? FindBestBlockOfSize(
        Dictionary<int, List<Seat>> seatsByRow,
        int size,
        HashSet<int> usedSeats,
        int totalRows,
        int middleRow,
        int maxSeatsPerRow,
        int middleSeat)
    {
        SeatBlock? bestBlock = null;
        double bestScore = double.MaxValue;

        foreach (var rowGroup in seatsByRow)
        {
            int row = rowGroup.Key;
            var rowSeats = rowGroup.Value.Where(s => !usedSeats.Contains(s.Id)).OrderBy(s => s.SeatNumber).ToList();

            for (int i = 0; i <= rowSeats.Count - size; i++)
            {
                bool isContiguous = true;
                for (int j = i; j < i + size - 1; j++)
                {
                    if (rowSeats[j + 1].SeatNumber != rowSeats[j].SeatNumber + 1)
                    {
                        isContiguous = false;
                        break;
                    }
                }

                if (!isContiguous) continue;

                var block = new List<Seat>();
                for (int j = i; j < i + size; j++)
                    block.Add(rowSeats[j]);

                double score = CalculateBlockScore(block, totalRows, middleRow, maxSeatsPerRow, middleSeat, isSplit: true);
                
                if (score < bestScore)
                {
                    bestScore = score;
                    bestBlock = new SeatBlock { Seats = block, Score = score };
                }
            }
        }

        return bestBlock?.Seats;
    }
    
    private static double CalculateBlockScore(
        List<Seat> block,
        int totalRows,
        int middleRow,
        int maxSeatsPerRow,
        int middleSeat,
        bool isSplit)
    {
        if (block.Count == 0) return double.MaxValue;

        var seat = block[0];
        double rowDistance = Math.Abs(seat.Row - middleRow) * RowDistanceWeight;
        
        double seatDistance = 0;
        foreach (var s in block)
            seatDistance += Math.Abs(s.SeatNumber - middleSeat) * SeatDistanceWeight;

        double orphanPenalty = CalculateOrphanPenalty(block, maxSeatsPerRow);
        double frontPenalty = IsFrontRow(seat.Row, middleRow) ? FrontRowPenalty * block.Count : 0;
        double sidePenalty = CalculateSidePenalty(block, maxSeatsPerRow);
        
        double score = rowDistance + seatDistance + orphanPenalty + frontPenalty + sidePenalty;

        if (isSplit)
            score += SplitGroupPenalty / block.Count;

        return score;
    }

    private static double CalculateOrphanPenalty(List<Seat> block, int maxSeatsPerRow)
    {
        if (block.Count == 0) return 0;

        var firstSeat = block[0];
        var lastSeat = block[block.Count - 1];
        double penalty = 0;

        if (firstSeat.SeatNumber == 1)
            penalty += 0;
        else if (firstSeat.SeatNumber - 1 > 0 && block.All(s => s.SeatNumber != firstSeat.SeatNumber - 1))
            penalty += OrphanSeatPenalty;

        if (lastSeat.SeatNumber == maxSeatsPerRow)
            penalty += 0;
        else if (lastSeat.SeatNumber + 1 <= maxSeatsPerRow && block.All(s => s.SeatNumber != lastSeat.SeatNumber + 1))
            penalty += OrphanSeatPenalty;

        return penalty;
    }

    private static double CalculateSidePenalty(List<Seat> block, int maxSeatsPerRow)
    {
        var firstSeat = block[0].SeatNumber;
        var lastSeat = block[block.Count - 1].SeatNumber;

        double leftRatio = firstSeat / (double)maxSeatsPerRow;
        double rightRatio = lastSeat / (double)maxSeatsPerRow;

        bool isExtremeLeft = leftRatio < (1.0 - ExtremeSideThreshold);
        bool isExtremeRight = rightRatio > ExtremeSideThreshold;

        return (isExtremeLeft || isExtremeRight) ? ExtremeSidePenalty * block.Count : 0;
    }

    private static bool IsFrontRow(int row, int middleRow)
    {
        return row <= FrontRowThreshold || row < middleRow / 2.0;
    }

    private static List<List<int>> GeneratePartitions(int n)
    {
        var partitions = new List<List<int>>();
        GeneratePartitionsHelper(n, n, new List<int>(), partitions);
        return partitions.OrderBy(p => p.Count).ToList();
    }

    private static void GeneratePartitionsHelper(int target, int max, List<int> current, List<List<int>> result)
    {
        if (target == 0)
        {
            result.Add(new List<int>(current));
            return;
        }

        for (int i = Math.Min(max, target); i >= 1; i--)
        {
            current.Add(i);
            GeneratePartitionsHelper(target - i, i, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }

    private static double CalculateGroupScore(
        List<List<Seat>> allocation,
        int totalRows,
        int middleRow,
        int maxSeatsPerRow,
        int middleSeat)
    {
        double totalScore = 0;
        foreach (var block in allocation)
        {
            totalScore += CalculateBlockScore(block, totalRows, middleRow, maxSeatsPerRow, middleSeat, allocation.Count > 1);
        }
        return totalScore;
    }

    private sealed class SeatBlock
    {
        public List<Seat> Seats { get; set; } = new();
        public double Score { get; set; }
    }

    public static int CalculateSeatScore(int row, int seatNumber, int totalRows, int seatsPerRow)
    {
        int middleRow = (totalRows + 1) / 2;
        int middleSeat = (seatsPerRow + 1) / 2;
        return (int)(Math.Abs(row - middleRow) * RowDistanceWeight + Math.Abs(seatNumber - middleSeat));
    }
}