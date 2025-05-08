using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using SecureMessaging.Models;
using SecureMessaging.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Device = SecureMessaging.Models.Device;

namespace SecureMessaging.ViewModels;

public partial class AppSettingsViewModel : ObservableObject
{
    private readonly DeviceService _deviceService;
    private readonly UserService _userService;
    private readonly AuthService _authService;

    [ObservableProperty]
    private ObservableCollection<Device> _devices;

    [ObservableProperty]
    private Device _currentDevice;

    [ObservableProperty]
    private bool _isRestricted;

    [ObservableProperty]
    private bool _isDarkTheme;

    public AppSettingsViewModel(
        DeviceService deviceService,
        UserService userService,
        AuthService authService)
    {
        _deviceService = deviceService;
        _userService = userService;
        _authService = authService;

        Devices = new ObservableCollection<Device>();
    }

    [RelayCommand]
    private async Task LoadSettings()
    {
        try
        {
            var userId = _authService.GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                await Shell.Current.DisplayAlert("Error", "User not authenticated", "OK");
                return;
            }

            // Load current device with retry logic
            Device currentDevice = null;
            int retries = 0;
            while (retries < 3 && currentDevice == null)
            {
                try
                {
                    currentDevice = await _deviceService.GetCurrentDevice();
                    retries++;
                }
                catch
                {
                    if (retries >= 2) throw;
                    await Task.Delay(1000);
                }
            }

            CurrentDevice = currentDevice ?? throw new Exception("Could not identify current device");

            // Load all devices
            var devices = await _deviceService.GetUserDevices(userId);

            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            // Load user restrictions
            var user = await _userService.GetUser(userId);
            IsRestricted = user.IsRestricted;

            // Load theme preference
            IsDarkTheme = Application.Current.UserAppTheme == AppTheme.Dark;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading settings: {ex}");
            await Shell.Current.DisplayAlert("Error",
                "Failed to load device settings. Please check your connection and try again.",
                "OK");
        }
    }

    [RelayCommand]
    private async Task RemoveDevice(Device device)
    {
        try
        {
            if (device == null) return;

            if (device.IsCurrent)
            {
                await Shell.Current.DisplayAlert("Error", "Cannot remove current device", "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm",
                $"Are you sure you want to remove {device.DeviceName}?",
                "Yes", "No");

            if (!confirm) return;

            await _deviceService.RemoveDevice(device.Id);
            Devices.Remove(device);

            await Shell.Current.DisplayAlert("Success", "Device removed successfully", "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing device: {ex}");
            await Shell.Current.DisplayAlert("Error", "Failed to remove device", "OK");
        }
    }

    [RelayCommand]
    private async Task SetPrimaryDevice(Device device)
    {
        if (device == null || device.IsPrimary) return;

        try
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Primary Device",
                $"Set {device.DeviceName} as your primary device?",
                "Confirm", "Cancel");

            if (!confirm) return;

            await _deviceService.SetPrimaryDevice(device.Id);
            await LoadSettings();

            await Shell.Current.DisplayAlert("Success",
                $"{device.DeviceName} is now your primary device",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error",
                ex.Message.Contains("not found") ? ex.Message :
                ex.Message.Contains("Network") ? ex.Message :
                "Failed to update device settings",
                "OK");
        }
    }

    [RelayCommand]
    private async Task ToggleRestrictions()
    {
        try
        {
            var userId = _authService.GetCurrentUserId();
            if (userId == Guid.Empty) return;

            await _userService.ToggleRestrictions(userId, IsRestricted);

            // Показываем сообщение об успехе
            await Shell.Current.DisplayAlert("Success", "Security settings updated", "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error toggling restrictions: {ex}");
            await Shell.Current.DisplayAlert("Error", "Failed to update security settings", "OK");
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        if (IsDarkTheme)
        {
            Application.Current.UserAppTheme = AppTheme.Dark;
        }
        else
        {
            Application.Current.UserAppTheme = AppTheme.Light;
        }
    }
}