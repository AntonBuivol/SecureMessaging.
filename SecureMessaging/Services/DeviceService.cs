using SecureMessaging.Models;
using Supabase.Postgrest;
using Supabase;
using Device = SecureMessaging.Models.Device;

namespace SecureMessaging.Services;

public class DeviceService
{
    private readonly Supabase.Client _supabase;

    public DeviceService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<List<Device>> GetUserDevices(Guid userId)
    {
        var response = await _supabase.From<Device>()
            .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
            .Order("is_current", Supabase.Postgrest.Constants.Ordering.Descending)
            .Order("is_primary", Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task RemoveDevice(Guid deviceId)
    {
        await _supabase.From<Device>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, deviceId)
            .Delete();
    }

    public async Task SetPrimaryDevice(Guid deviceId)
    {
        var device = await _supabase.From<Device>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, deviceId)
            .Single();

        if (device != null)
        {
            device.IsPrimary = true;
            await _supabase.From<Device>().Update(device);
        }
    }
}