using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BioscoopCasus.API.Data;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly BioscoopDbContext _context;

    public RoomsController(BioscoopDbContext context)
    {
        _context = context;
    }

    // GET /api/rooms
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomResponseDto>>> GetRooms()
    {
        var rooms = await _context.Rooms
            .Include(r => r.Rows)
            .OrderBy(r => r.Number)
            .Select(r => new RoomResponseDto(
                r.Id,
                r.Number,
                r.Name,
                r.Has3D,
                r.IsWheelchairAccessible,
                r.Rows.OrderBy(row => row.RowNumber).Select(row => new RowResponseDto(
                    row.Id,
                    row.RowNumber,
                    row.SeatCount
                )).ToList()
            ))
            .ToListAsync();

        return Ok(rooms);
    }

    // GET /api/rooms/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<RoomResponseDto>> GetRoom(int id)
    {
        var room = await _context.Rooms
            .Include(r => r.Rows)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null)
            return NotFound();

        var response = new RoomResponseDto(
            room.Id,
            room.Number,
            room.Name,
            room.Has3D,
            room.IsWheelchairAccessible,
            room.Rows.OrderBy(row => row.RowNumber).Select(row => new RowResponseDto(
                row.Id,
                row.RowNumber,
                row.SeatCount
            )).ToList()
        );

        return Ok(response);
    }

    // POST /api/rooms
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<RoomResponseDto>> CreateRoom(RoomCreateDto dto)
    {
        // First check if a room with this number already exists
        if (await _context.Rooms.AnyAsync(r => r.Number == dto.Number))
        {
            return BadRequest($"Room number {dto.Number} already exists.");
        }

        var room = new Room
        {
            Number = dto.Number,
            Name = dto.Name,
            Has3D = dto.Has3D,
            IsWheelchairAccessible = dto.IsWheelchairAccessible
        };

        // Create rows based on the DTO layout
        foreach (var rowDto in dto.Rows)
        {
            room.Rows.Add(new Row
            {
                RoomId = room.Id,
                RowNumber = rowDto.RowNumber,
                SeatCount = rowDto.SeatCount
            });
        }

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();

        var response = new RoomResponseDto(
            room.Id,
            room.Number,
            room.Name,
            room.Has3D,
            room.IsWheelchairAccessible,
            room.Rows.OrderBy(x => x.RowNumber).Select(row => new RowResponseDto(
                row.Id,
                row.RowNumber,
                row.SeatCount
            )).ToList()
        );

        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, response);
    }

    // PUT /api/rooms/{id}
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateRoom(int id, RoomUpdateDto dto)
    {
        var room = await _context.Rooms
            .Include(r => r.Rows)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null)
            return NotFound();

        // Check if updating to a number that is already taken by ANOTHER room
        if (room.Number != dto.Number && await _context.Rooms.AnyAsync(r => r.Number == dto.Number))
        {
            return BadRequest($"Room number {dto.Number} is already taken by another room.");
        }

        room.Number = dto.Number;
        room.Name = dto.Name;
        room.Has3D = dto.Has3D;
        room.IsWheelchairAccessible = dto.IsWheelchairAccessible;

        // Easiest way to update rows: clear existing and insert new ones
        _context.Rows.RemoveRange(room.Rows);
        room.Rows.Clear();

        foreach (var rowDto in dto.Rows)
        {
            room.Rows.Add(new Row
            {
                RoomId = room.Id,
                RowNumber = rowDto.RowNumber,
                SeatCount = rowDto.SeatCount
            });
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/rooms/{id}
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room == null)
            return NotFound();

        _context.Rooms.Remove(room);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
