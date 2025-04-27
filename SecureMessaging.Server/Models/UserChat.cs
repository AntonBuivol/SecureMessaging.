using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SecureMessaging.Server.Models;

[Table("user_chats")]
public class UserChat : BaseModel
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("chat_id")]
    public Guid ChatId { get; set; }
}