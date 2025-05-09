﻿using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SecureMessaging.Models;

[Table("messages")]
public class Message : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("chat_id")]
    public Guid ChatId { get; set; }

    [Column("sender_id")]
    public Guid SenderId { get; set; }

    [Column("content")]
    public string Content { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Клиентское свойство (не из БД)
    public bool IsCurrentUser { get; set; }
    public string SenderName { get; set; }
    public string DisplayTime => CreatedAt.ToString("HH:mm");
}