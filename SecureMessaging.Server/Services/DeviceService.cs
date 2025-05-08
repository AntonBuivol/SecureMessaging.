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

    public async Task<List<DeviceDto>> GetUserDevices(Guid userId)
    {
        var response = await _supabase
            .From<Device>()
            .Where(x => x.UserId == userId)
            .Order(x => x.IsCurrent, Supabase.Postgrest.Constants.Ordering.Descending)
            .Order(x => x.IsPrimary, Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(d => new DeviceDto
        {
            Id = d.Id,
            UserId = d.UserId,
            DeviceName = d.DeviceName,
            DeviceInfo = d.DeviceInfo,
            IsPrimary = d.IsPrimary,
            IsCurrent = d.IsCurrent,
            CreatedAt = d.CreatedAt,
            LastActive = d.LastActive
        }).ToList();
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
        // First reset all primary flags
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

    public async Task<DeviceDto> GetCurrentDevice(Guid userId, string deviceName)
    {
        try
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                // Find the device marked as current for this user
                var response = await _supabase.From<Device>()
                    .Where(x => x.UserId == userId && x.IsCurrent)
                    .Single();

                if (response == null)
                {
                    throw new KeyNotFoundException($"No current device found for user {userId}");
                }

                return ToDeviceDto(response);
            }

            var device = await _supabase.From<Device>()
                .Where(x => x.UserId == userId && x.DeviceName == deviceName)
                .Single();

            if (device == null)
            {
                throw new KeyNotFoundException($"Device not found for user {userId} with name {deviceName}");
            }

            return ToDeviceDto(device);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting current device for user {userId}   {ex}");
            throw;
        }
    }

    private DeviceDto ToDeviceDto(Device device)
    {
        return new DeviceDto
        {
            Id = device.Id,
            UserId = device.UserId,
            DeviceName = device.DeviceName,
            DeviceInfo = device.DeviceInfo,
            IsPrimary = device.IsPrimary,
            IsCurrent = device.IsCurrent,
            CreatedAt = device.CreatedAt,
            LastActive = device.LastActive
        };
    }
}