using System.Net.Http.Json;
using BioscoopCasus.Models.DTOs;

namespace BioscoopCasus.Web.Services;

public class SeatSelectionService(HttpClient httpClient)
{
    public async Task<List<MovieResponseDto>?> GetMoviesAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("api/movies");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<MovieResponseDto>>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching movies: {ex.Message}");
        }
        return null;
    }

    public async Task<List<ShowtimeResponseDto>?> GetShowtimesForMovieAsync(int movieId)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/showtimes?movieId={movieId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ShowtimeResponseDto>>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching showtimes: {ex.Message}");
        }
        return null;
    }

    public async Task<List<SeatInfoDto>?> GetAvailableSeatsAsync(int showtimeId)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/seat-selection/{showtimeId}/available");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<SeatInfoDto>>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching available seats: {ex.Message}");
        }
        return null;
    }

    public async Task<SeatSelectionResponseDto?> SuggestSeatsAsync(int showtimeId, int groupSize)
    {
        try
        {
            var request = new SeatSelectionRequestDto(groupSize);
            var response = await httpClient.PostAsJsonAsync($"api/seat-selection/{showtimeId}/suggest", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SeatSelectionResponseDto>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error suggesting seats: {ex.Message}");
        }
        return null;
    }

    public async Task<ReservationConfirmResponseDto?> ConfirmReservationAsync(int showtimeId, List<int> seatIds)
    {
        try
        {
            var request = new ReservationConfirmRequestDto(seatIds);
            var response = await httpClient.PostAsJsonAsync($"api/seat-selection/{showtimeId}/reserve", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ReservationConfirmResponseDto>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error confirming reservation: {ex.Message}");
        }
        return null;
    }
}
