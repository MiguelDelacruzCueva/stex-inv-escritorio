using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Lista de operarios del almacén vinculado. Funciona sin conexión.
/// </summary>
public partial class OperatorSelectionViewModel : ViewModelBase
{
    private readonly ILocalStore _localStore;
    private readonly IWarehouseSyncService _syncService;
    private readonly IDeviceSessionService _deviceSession;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    private List<Operario> _allOperators = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _sessionExpired;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _warehouseName = "Almacén";

    public ObservableCollection<Operario> Operators { get; } = new();

    public OperatorSelectionViewModel(
        ILocalStore localStore,
        IWarehouseSyncService syncService,
        IDeviceSessionService deviceSession,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _localStore = localStore;
        _syncService = syncService;
        _deviceSession = deviceSession;
        _navigation = navigation;
        _dialogs = dialogs;
    }

    public override Task InitializeAsync(object? parameter = null) => ReloadAsync();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private async Task ReloadAsync()
    {
        var state = await _localStore.LoadAsync();

        WarehouseName = state.Device?.AlmacenDisplay ?? "Almacén";
        SessionExpired = state.Device?.IsTokenExpired ?? false;
        StatusMessage = state.LastSyncError ?? string.Empty;

        if (SessionExpired && string.IsNullOrWhiteSpace(StatusMessage))
        {
            StatusMessage = "La sesión del almacén venció. Puedes seguir usando los PIN guardados; " +
                            "un administrador debe re-vincular el equipo para sincronizar.";
        }

        HasStatusMessage = !string.IsNullOrWhiteSpace(StatusMessage) || SessionExpired;

        _allOperators = state.Operarios
            .Where(o => o.Activo)
            .OrderBy(o => o.NombreCompleto)
            .ToList();

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var term = SearchText?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(term)
            ? _allOperators
            : _allOperators
                .Where(o => o.NombreCompleto.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Operators.Clear();
        foreach (var operario in filtered)
        {
            Operators.Add(operario);
        }

        IsEmpty = Operators.Count == 0;
    }

    [RelayCommand]
    private async Task SelectOperatorAsync(Operario? operario)
    {
        if (operario is null)
        {
            return;
        }

        await _navigation.NavigateToAsync<PinEntryViewModel>(operario);
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAsync()
    {
        IsBusy = true;

        try
        {
            var result = await _syncService.SyncAsync();

            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage ?? "No se pudo sincronizar el almacén.";
                HasStatusMessage = true;
            }

            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSync() => !IsBusy;

    [RelayCommand]
    private async Task ReenrollAsync()
    {
        await _navigation.NavigateToAsync<EnrollmentViewModel>();
    }

    [RelayCommand]
    private async Task UnbindAsync()
    {
        var confirmed = _dialogs.Confirm(
            "Desvincular equipo",
            "Se eliminarán la sesión del almacén y los operarios guardados en este equipo. " +
            "Tendrás que ingresar las credenciales del almacén nuevamente. ¿Continuar?");

        if (!confirmed)
        {
            return;
        }

        await _deviceSession.UnbindAsync();
        await _navigation.NavigateToAsync<EnrollmentViewModel>();
    }
}
