namespace BioscoopCasus.Models.DTOs;

public record PopcornOrderDto(
    string Size,
    string Flavor,
    bool AddDrink,
    bool AddRefill);

public record PopcornOrderResponseDto(
    int Id,
    string Size,
    string Flavor,
    bool AddDrink,
    bool AddRefill);
