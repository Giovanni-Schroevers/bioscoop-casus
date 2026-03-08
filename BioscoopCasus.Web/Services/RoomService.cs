using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class RoomService
{
    private readonly HttpClient _http;

    public RoomService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<RoomResponseDto>> GetRoomsAsync()
    {
        var result = await _http.GetFromJsonAsync<IEnumerable<RoomResponseDto>>("api/rooms");
        return result ?? Enumerable.Empty<RoomResponseDto>();
    }

    public async Task<RoomResponseDto?> GetRoomAsync(int id)
    {
        return await _http.GetFromJsonAsync<RoomResponseDto>($"api/rooms/{id}");
    }
}
