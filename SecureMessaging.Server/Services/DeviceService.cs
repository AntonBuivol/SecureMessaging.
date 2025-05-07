using SecureMessaging.Server.Models;
using Supabase;
using Supabase.Postgrest;

namespace SecureMessaging.Server.Services;

public class DeviceService
{
    private readonly Supabase.Client _supabase;

    public DeviceService(Supabase.Client supabase)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
    }

    public async Task CreateDevice(Guid userId, string deviceName, string deviceInfo, bool isPrimary, bool isCurrent)
    {
        try
        {
            // 1. Verify Supabase initialization
            if (_supabase == null)
            {
                throw new Exception("Supabase client is null");
            }

            // 2. Explicitly initialize the table reference
            var table = _supabase.From<Device>();
            if (table == null)
            {
                throw new Exception("Failed to initialize Device table");
            }

            // 3. Create device with all required fields
            var newDevice = new Device
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceName = deviceName ?? "Unknown",
                DeviceInfo = deviceInfo ?? "Unknown",
                IsPrimary = isPrimary,
                IsCurrent = isCurrent,
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow,
                AccessToken = string.Empty // Required field
            };

            // 4. Insert with explicit error handling
            var response = await table.Insert(newDevice);

            if (!response.ResponseMessage.IsSuccessStatusCode)
            {
                var error = await response.ResponseMessage.Content.ReadAsStringAsync();
                throw new Exception($"Supabase error: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Device creation error: {ex}");
            throw;
        }
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