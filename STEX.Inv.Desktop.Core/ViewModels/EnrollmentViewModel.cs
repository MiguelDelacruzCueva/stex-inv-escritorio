using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Vinculación del equipo con la cuenta del almacén. Se ejecuta una sola vez
/// (o cuando un administrador decide re-vincular).
/// </summary>
public partial class EnrollmentViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IWarehouseSyncService _syncService;
    private readonly IDeviceSessionService _deviceSession;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido.")]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "Mínimo 6 caracteres.")]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnrollCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public EnrollmentViewModel(
        IAuthService authService,
        IWarehouseSyncService syncService,
        IDeviceSessionService deviceSession,
        INavigationService navigation)
    {
        _authService = authService;
        _syncService = syncService;
        _deviceSession = deviceSession;
        _navigation = navigation;
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        var device = await _deviceSession.GetAsync();
        if (device is not null && string.IsNullOrWhiteSpace(Email))
        {
            Email = device.Email;
        }
    }

    private bool CanEnroll() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanEnroll))]
    private async Task EnrollAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Validando credenciales...";

        try
        {
            var login = await _authService.LoginAsync(Email.Trim(), Password);

            if (!login.IsSuccess)
            {
                ErrorMessage = login.ErrorMessage ?? "No se pudo vincular el equipo.";
                return;
            }

            var session = new DeviceSession
            {
                ExpiresAtUtc = login.ExpiresAtUtc ?? default,
                AlmacenId = login.User?.AlmacenId,
                AlmacenNombre = login.User?.AlmacenNombre ?? string.Empty,
                AlmacenCodigo = login.User?.AlmacenCodigo ?? string.Empty,
                Email = Email.Trim(),
                Rol = login.User?.Rol ?? string.Empty,
                BoundAtUtc = DateTimeOffset.UtcNow
            };

            await _deviceSession.SaveAsync(session);

            Password = string.Empty;
            StatusMessage = "Sincronizando datos del almacén...";

            // Si falla la sync el equipo queda vinculado igualmente; la pantalla
            // de operarios mostrará el error y permitirá reintentar.
            await _syncService.SyncAsync();

            await _navigation.NavigateToAsync<OperatorSelectionViewModel>();
        }
        catch (Exception)
        {
            ErrorMessage = "Ocurrió un error inesperado al vincular el equipo.";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }
}
