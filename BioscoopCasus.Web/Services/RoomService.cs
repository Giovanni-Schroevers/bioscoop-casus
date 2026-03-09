using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class RoomService(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<IEnumerable<RoomResponseDto>> GetRoomsAsync()
    {
        var result = await _http.GetFromJsonAsync<IEnumerable<RoomResponseDto>>("api/rooms");
        return result ?? Enumerable.Empty<RoomResponseDto>();
    }

    public async Task<RoomResponseDto?> GetRoomAsync(int id)
    {
        return await _http.GetFromJsonAsync<RoomResponseDto>($"api/rooms/{id}");
    }

    public async Task<bool> CreateRoomAsync(RoomCreateDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/rooms", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateRoomAsync(int id, RoomUpdateDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/rooms/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteRoomAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/rooms/{id}");
        return response.IsSuccessStatusCode;
    }
}
