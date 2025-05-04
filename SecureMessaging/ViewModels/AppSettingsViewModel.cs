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
        var userId = _authService.GetCurrentUserId();

        // Load devices
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

    [RelayCommand]
    private async Task RemoveDevice(Device device)
    {
        if (device.IsCurrent)
        {
            await Shell.Current.DisplayAlert("Error", "Cannot remove current device", "OK");
            return;
        }

        await _deviceService.RemoveDevice(device.Id);
        Devices.Remove(device);
    }

    [RelayCommand]
    private async Task SetPrimaryDevice(Device device)
    {
        await _deviceService.SetPrimaryDevice(device.Id);
        await LoadSettings();
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