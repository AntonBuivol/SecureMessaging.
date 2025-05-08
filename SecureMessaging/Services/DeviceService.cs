using SecureMessaging.Models;
using Supabase.Postgrest;
using Supabase;
using Device = SecureMessaging.Models.Device;
using static Supabase.Postgrest.Constants;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;

namespace SecureMessaging.Services;

public class DeviceService
{
    private readonly Supabase.Client _supabase;
    private readonly AuthService _authService;

    public DeviceService(Supabase.Client supabase, AuthService authService)
    {
        _supabase = supabase;
        _authService = authService;
    }

    public async Task<List<Device>> GetUserDevices(Guid userId)
    {
        try
        {
            await _authService.EnsureHubConnected(requireAuth: true);
            return await _authService.HubConnection.InvokeAsync<List<Device>>("GetUserDevices");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting user devices: {ex}");
            return new List<Device>();
        }
    }

    public async Task RemoveDevice(Guid deviceId)
    {
        try
        {
            await _authService.EnsureHubConnected(requireAuth: true);
            await _authService.HubConnection.InvokeAsync("RemoveDevice", deviceId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing device: {ex}");
            throw;
        }
    }

    public async Task SetPrimaryDevice(Guid deviceId)
    {
        try
        {
            await _authService.EnsureHubConnected(requireAuth: true);
            await _authService.HubConnection.InvokeAsync("SetPrimaryDevice", deviceId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting primary device: {ex}");
            throw;
        }
    }

    public async Task<Device> GetCurrentDevice()
    {
        try
        {
            await _authService.EnsureHubConnected(requireAuth: true);
            var device = await _authService.HubConnection.InvokeAsync<Device>("GetCurrentDevice");

            if (device == null)
            {
                throw new Exception("Current device information not available");
            }

            return device;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting current device: {ex}");
            throw new Exception("Could not identify current device. Please check your connection and try again.", ex);
        }
    }
}