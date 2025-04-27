using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SecureMessaging.Server.Models;

[Table("users")]
public class User : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("username")]
    public string Username { get; set; }

    // Только для сервера
    [Column("password_hash")]
    public string PasswordHash { get; set; }

    [Column("display_name")]
    public string DisplayName { get; set; }

    [Column("about")]
    public string About { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("is_restricted")]
    public bool IsRestricted { get; set; }
}