namespace SecureMessaging.Server.Models
{
    public class DeviceDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceInfo { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsCurrent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActive { get; set; }
    }

}
