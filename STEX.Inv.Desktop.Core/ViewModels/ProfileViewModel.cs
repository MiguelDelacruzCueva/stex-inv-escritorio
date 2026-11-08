using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Datos del operario y del equipo vinculado.
/// </summary>
public partial class ProfileViewModel : ViewModelBase
{
    private readonly IOperatorSession _operatorSession;
    private readonly IDeviceSessionService _deviceSession;
    private readonly ILocalStore _localStore;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _themeService;
    private bool _loadingTheme;

    [ObservableProperty]
    private string _operatorName = "Operario";

    [ObservableProperty]
    private string _initials = "?";

    [ObservableProperty]
    private string _warehouseName = "Almacén";

    [ObservableProperty]
    private string _email = "—";

    [ObservableProperty]
    private string _role = "—";

    [ObservableProperty]
    private string _boundAtText = "—";

    [ObservableProperty]
    private string _expiresAtText = "—";

    [ObservableProperty]
    private string _lastSyncText = "Sin sincronizar";

    [ObservableProperty]
    private string _sessionStatus = "Sesión activa";

    [ObservableProperty]
    private bool _isSessionExpired;

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemeMode.Light, "Claro"),
        new(ThemeMode.Dark, "Oscuro"),
        new(ThemeMode.System, "Sistema")
    ];

    public ProfileViewModel(
        IOperatorSession operatorSession,
        IDeviceSessionService deviceSession,
        ILocalStore localStore,
        INavigationService navigation,
        IDialogService dialogs,
        IThemeService themeService)
    {
        _operatorSession = operatorSession;
        _deviceSession = deviceSession;
        _localStore = localStore;
        _navigation = navigation;
        _dialogs = dialogs;
        _themeService = themeService;
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        var operatorSession = _operatorSession.Current;
        OperatorName = operatorSession?.NombreCompleto ?? "Operario";
        Initials = operatorSession?.Iniciales ?? "?";

        var device = await _deviceSession.GetAsync();
        var state = await _localStore.LoadAsync();

        WarehouseName = device?.AlmacenDisplay ?? "Almacén";
        Email = string.IsNullOrWhiteSpace(device?.Email) ? "—" : device.Email;
        Role = string.IsNullOrWhiteSpace(device?.Rol) ? "—" : device.Rol;
        BoundAtText = device is null
            ? "—"
            : device.BoundAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        ExpiresAtText = device is null || device.ExpiresAtUtc == default
            ? "—"
            : device.ExpiresAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        LastSyncText = state.LastSyncUtc is { } last
            ? last.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Sin sincronizar";

        IsSessionExpired = device?.IsTokenExpired ?? false;
        SessionStatus = IsSessionExpired ? "Sesión vencida" : "Sesión activa";

        _loadingTheme = true;
        SelectedTheme = ThemeOptions.FirstOrDefault(t => t.Mode == _themeService.Current)
                        ?? ThemeOptions[^1];
        _loadingTheme = false;
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_loadingTheme || value is null)
        {
            return;
        }

        _ = ApplyThemeAsync(value.Mode);
    }

    private async Task ApplyThemeAsync(ThemeMode mode)
    {
        try
        {
            await _themeService.SetThemeAsync(mode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo aplicar el tema: {ex}");
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _operatorSession.SignOut();
        await _navigation.NavigateToAsync<OperatorSelectionViewModel>();
    }

    [RelayCommand]
    private async Task UnbindAsync()
    {
        var confirmed = _dialogs.Confirm(
            "Desvincular equipo",
            "Se eliminarán la sesión del almacén, los operarios y los conteos guardados en este equipo. " +
            "¿Continuar?");

        if (!confirmed)
        {
            return;
        }

        _operatorSession.SignOut();
        await _deviceSession.UnbindAsync();
        await _navigation.NavigateToAsync<EnrollmentViewModel>();
    }
}

public sealed record ThemeOption(ThemeMode Mode, string Label);
