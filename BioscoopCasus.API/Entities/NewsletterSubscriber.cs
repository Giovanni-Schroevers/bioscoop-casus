namespace BioscoopCasus.API.Entities;

public class NewsletterSubscriber
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Name { get; set; }

    public bool ConfirmationSent { get; set; }

    public DateTime? LatestReceivedEmail { get; set; }
}