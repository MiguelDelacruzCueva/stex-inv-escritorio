using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Firma del operario con PIN de 4 dígitos. La validación es 100% local.
/// </summary>
public partial class PinEntryViewModel : ViewModelBase
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(60);

    private readonly ILocalStore _localStore;
    private readonly IPinHasher _pinHasher;
    private readonly IOperatorSession _operatorSession;
    private readonly INavigationService _navigation;

    private Operario? _operario;

    [ObservableProperty]
    private string _operatorName = string.Empty;

    [ObservableProperty]
    private string _initials = "?";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinDots))]
    private string _pin = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private string _lockMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DigitCommand))]
    [NotifyCanExecuteChangedFor(nameof(BackspaceCommand))]
    private bool _isBusy;

    public string PinDots => new string('●', Pin.Length) + new string('○', Math.Max(0, 4 - Pin.Length));

    public PinEntryViewModel(
        ILocalStore localStore,
        IPinHasher pinHasher,
        IOperatorSession operatorSession,
        INavigationService navigation)
    {
        _localStore = localStore;
        _pinHasher = pinHasher;
        _operatorSession = operatorSession;
        _navigation = navigation;
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        if (parameter is not Operario operario)
        {
            ErrorMessage = "Operario no válido.";
            return;
        }

        _operario = operario;
        OperatorName = operario.NombreCompleto;
        Initials = operario.Iniciales;

        await RefreshLockStateAsync();
    }

    private async Task RefreshLockStateAsync()
    {
        if (_operario is null)
        {
            return;
        }

        var state = await _localStore.LoadAsync();

        if (state.PinAttempts.TryGetValue(_operario.Id, out var attempt)
            && attempt.LockedUntilUtc is { } until
            && until > DateTimeOffset.UtcNow)
        {
            IsLocked = true;
            LockMessage = $"Demasiados intentos. Espera {RemainingSeconds(until)} s y vuelve a seleccionar el operario.";
        }
        else
        {
            IsLocked = false;
            LockMessage = string.Empty;
        }
    }

    private bool CanInput() => !IsBusy && !IsLocked;

    [RelayCommand(CanExecute = nameof(CanInput))]
    private async Task DigitAsync(string? digit)
    {
        if (IsBusy || IsLocked)
        {
            return;
        }

        if (_operario is null || Pin.Length >= 4)
        {
            return;
        }

        if (digit is null || digit.Length != 1 || !char.IsAsciiDigit(digit[0]))
        {
            return;
        }

        Pin += digit;

        if (Pin.Length == 4)
        {
            await SubmitAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInput))]
    private void Backspace()
    {
        if (Pin.Length > 0)
        {
            Pin = Pin[..^1];
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Pin = string.Empty;
        ErrorMessage = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanInput))]
    private async Task SubmitAsync()
    {
        if (IsBusy || IsLocked || _operario is null || Pin.Length != 4)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var state = await _localStore.LoadAsync();
            var operario = state.Operarios.FirstOrDefault(o => o.Id == _operario.Id);

            if (operario is null)
            {
                ErrorMessage = "El operario ya no está disponible. Sincroniza el almacén.";
                Pin = string.Empty;
                return;
            }

            if (!operario.TienePin)
            {
                ErrorMessage = "El operario no tiene PIN configurado.";
                Pin = string.Empty;
                return;
            }

            if (_pinHasher.Verify(Pin, operario.PinSalt, operario.PinHash))
            {
                state.PinAttempts.Remove(operario.Id);
                await _localStore.SaveAsync(state);

                _operatorSession.SignIn(operario);
                Pin = string.Empty;

                await _navigation.NavigateToAsync<ShellViewModel>();
                return;
            }

            if (!state.PinAttempts.TryGetValue(operario.Id, out var attempt))
            {
                attempt = new PinAttempt();
                state.PinAttempts[operario.Id] = attempt;
            }

            attempt.FailedCount++;

            if (attempt.FailedCount >= MaxAttempts)
            {
                attempt.LockedUntilUtc = DateTimeOffset.UtcNow.Add(LockDuration);
                attempt.FailedCount = 0;
                await _localStore.SaveAsync(state);

                Pin = string.Empty;
                IsLocked = true;
                LockMessage = $"Demasiados intentos. Espera {RemainingSeconds(attempt.LockedUntilUtc.Value)} s y vuelve a seleccionar el operario.";
                return;
            }

            await _localStore.SaveAsync(state);

            Pin = string.Empty;
            ErrorMessage = $"PIN incorrecto. {MaxAttempts - attempt.FailedCount} intento(s) restante(s).";
        }
        catch (Exception)
        {
            ErrorMessage = "No se pudo validar el PIN. Inténtalo de nuevo.";
            Pin = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await _navigation.NavigateToAsync<OperatorSelectionViewModel>();
    }

    private static int RemainingSeconds(DateTimeOffset until)
    {
        var remaining = (until - DateTimeOffset.UtcNow).TotalSeconds;
        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
    }
}
