using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureMessaging.Models
{
    public class ChatWithParticipants
    {
        public Chat Chat { get; set; }
        public List<User> Participants { get; set; }
    }
}
