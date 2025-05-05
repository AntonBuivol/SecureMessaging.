using SecureMessaging.Server.Models;
using Supabase;

namespace SecureMessaging.Server.Services;

public class DeviceService
{
    private readonly Supabase.Client _supabase;

    public DeviceService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task CreateDevice(Guid userId, string deviceName, string deviceInfo, bool isPrimary, bool isCurrent)
    {
        // Reset current device flag for all other devices of this user
        if (isCurrent)
        {
            var currentDevices = await _supabase
                .From<Device>()
                .Where(x => x.UserId == userId && x.IsCurrent)
                .Get();

            foreach (var device in currentDevices.Models)
            {
                device.IsCurrent = false;
                await _supabase.From<Device>().Update(device);
            }
        }

        // Create new device
        var newDevice = new Device
        {
            UserId = userId,
            DeviceName = deviceName,
            DeviceInfo = deviceInfo,
            IsPrimary = isPrimary,  // This will be false for login-created devices
            IsCurrent = isCurrent,
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow
        };

        await _supabase.From<Device>().Insert(newDevice);
    }

    public async Task<List<Device>> GetUserDevices(Guid userId)
    {
        var response = await _supabase
            .From<Device>()
            .Where(x => x.UserId == userId)
            .Order(x => x.IsCurrent, Supabase.Postgrest.Constants.Ordering.Descending)
            .Order(x => x.IsPrimary, Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task RemoveDevice(Guid deviceId, Guid userId)
    {
        await _supabase
            .From<Device>()
            .Where(x => x.Id == deviceId && x.UserId == userId)
            .Delete();
    }

    public async Task SetPrimaryDevice(Guid deviceId, Guid userId)
    {
        // Reset primary flag for all devices
        var devices = await _supabase
            .From<Device>()
            .Where(x => x.UserId == userId && x.IsPrimary)
            .Get();

        foreach (var device in devices.Models)
        {
            device.IsPrimary = false;
            await _supabase.From<Device>().Update(device);
        }

        // Set new primary device
        var primaryDevice = await _supabase
            .From<Device>()
            .Where(x => x.Id == deviceId && x.UserId == userId)
            .Single();

        if (primaryDevice != null)
        {
            primaryDevice.IsPrimary = true;
            await _supabase.From<Device>().Update(primaryDevice);
        }
    }
}