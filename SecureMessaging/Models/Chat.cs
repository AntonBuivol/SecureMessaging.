using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SecureMessaging.Models;

[Table("chats")]
public class Chat : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("is_group")]
    public bool IsGroup { get; set; }

    [Column("group_name")]
    public string GroupName { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("last_message_at")]
    public DateTime? LastMessageAt { get; set; }

    // Клиентское свойство (не из БД)
    public string DisplayName { get; set; }
}