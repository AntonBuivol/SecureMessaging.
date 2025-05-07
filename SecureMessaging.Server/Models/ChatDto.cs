namespace SecureMessaging.Server.Models
{
    public class ChatDto
    {
        public Guid Id { get; set; }
        public bool IsGroup { get; set; }
        public string GroupName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public string DisplayName { get; set; }
    }
}
