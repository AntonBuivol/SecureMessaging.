using Newtonsoft.Json;

namespace SecureMessaging.Server.Models
{
    public class ChatWithParticipantsResult
    {
        [JsonProperty("chat")]
        public Chat Chat { get; set; }

        [JsonProperty("participants")]
        public List<User> Participants { get; set; }
    }
}
