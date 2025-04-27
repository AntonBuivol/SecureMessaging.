using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SecureMessaging.Models;

[Table("devices")]
public class Device : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("device_name")]
    public string DeviceName { get; set; }

    [Column("device_info")]
    public string DeviceInfo { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("is_current")]
    public bool IsCurrent { get; set; }

    [Column("access_token")]
    public string AccessToken { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("last_active")]
    public DateTime LastActive { get; set; }
}