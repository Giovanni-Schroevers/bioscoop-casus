using System.ComponentModel.DataAnnotations;

namespace BioscoopCasus.API.Entities;

public class MovieTranslation
{
    public int Id { get; set; }
    
    [Required]
    public int MovieId { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; }
    
    public string Description { get; set; }
    
    public string Genres { get; set; }
    
    // Navigation property
    public Movie Movie { get; set; }
}
