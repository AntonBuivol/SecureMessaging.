﻿using SecureMessaging.Server.Models;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using static Supabase.Postgrest.Constants;

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
        try
        {
            await _supabase.Rpc("set_primary_device",
                new Dictionary<string, object>
                {
                { "p_device_id", deviceId },
                { "p_user_id", userId }
                });
        }
        catch (PostgrestException ex)
        {
            Console.WriteLine($"Supabase error: {ex.Message}");
            if (ex.Message.Contains("Device not found"))
            {
                throw new KeyNotFoundException(ex.Message);
            }
            throw new Exception("Database operation failed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting primary device: {ex}");
            throw;
        }
    }

    public async Task<DeviceDto> GetCurrentDevice(Guid userId, string deviceName)
    {
        try
        {
            // First try exact match
            var exactMatch = await _supabase.From<Device>()
                .Where(x => x.UserId == userId && x.DeviceName == deviceName)
                .Single();

            if (exactMatch != null)
            {
                return ToDeviceDto(exactMatch);
            }

            // Try partial match if exact match fails
            var devices = await _supabase.From<Device>()
                .Where(x => x.UserId == userId)
                .Get();

            var matchingDevice = devices.Models
                .FirstOrDefault(d => d.DeviceName.Contains(deviceName) ||
                                 deviceName.Contains(d.DeviceName));

            if (matchingDevice != null)
            {
                return ToDeviceDto(matchingDevice);
            }

            // Fallback to most recent device
            var mostRecent = devices.Models
                .OrderByDescending(d => d.LastActive)
                .FirstOrDefault();

            if (mostRecent != null)
            {
                return ToDeviceDto(mostRecent);
            }

            throw new KeyNotFoundException($"No devices found for user {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting current device for user {userId}   -   {ex}");
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