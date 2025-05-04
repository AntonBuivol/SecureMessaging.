namespace SecureMessaging.Server.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }

    // Клиентские свойства
    public bool IsCurrentUser { get; set; }
    public string SenderName { get; set; }
    public string DisplayTime => CreatedAt.ToString("HH:mm");
}