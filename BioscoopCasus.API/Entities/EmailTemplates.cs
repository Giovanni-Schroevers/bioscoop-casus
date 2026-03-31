namespace BioscoopCasus.API.Entities;

public class EmailTemplates
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }

    public DateTime? ChangedOn { get; set; }
}