namespace SecureMessaging.Server.Models
{
    public class ChatWithParticipants
    {
        public Chat Chat { get; set; }
        public List<User> Participants { get; set; }
    }
}
