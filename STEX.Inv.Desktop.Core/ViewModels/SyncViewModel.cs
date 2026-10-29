using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Descarga de maestros y estado de la copia local del almacén.
/// </summary>
public partial class SyncViewModel : ViewModelBase
{
    private readonly ILocalStore _localStore;
    private readonly IWarehouseSyncService _syncService;

    [ObservableProperty]
    private string _warehouseName = "Almacén";

    [ObservableProperty]
    private string _lastSyncText = "Sin sincronizar";

    [ObservableProperty]
    private int _materialesCount;

    [ObservableProperty]
    private int _operariosCount;

    [ObservableProperty]
    private int _campanasCount;

    [ObservableProperty]
    private int _conteosCount;

    [ObservableProperty]
    private int _conteosPendientes;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    private bool _isBusy;

    public SyncViewModel(ILocalStore localStore, IWarehouseSyncService syncService)
    {
        _localStore = localStore;
        _syncService = syncService;
    }

    public override Task InitializeAsync(object? parameter = null) => LoadAsync();

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAsync()
    {
        IsBusy = true;

        try
        {
            var result = await _syncService.SyncAsync();

            if (!result.IsSuccess)
            {
                SetStatus(result.ErrorMessage ?? "No se pudo sincronizar el equipo.", true);
                return;
            }

            await LoadAsync();
            SetStatus(
                result.UploadedCount switch
                {
                    0 => "Datos maestros actualizados correctamente.",
                    1 => "Se subió 1 conteo y se actualizaron los datos maestros.",
                    _ => $"Se subieron {result.UploadedCount} conteos y se actualizaron los datos maestros."
                },
                false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSync() => !IsBusy;

    private async Task LoadAsync()
    {
        var state = await _localStore.LoadAsync();

        WarehouseName = state.Device?.AlmacenDisplay ?? "Almacén";
        MaterialesCount = state.Materiales.Count;
        OperariosCount = state.Operarios.Count(o => o.Activo);
        CampanasCount = state.Campanas.Count;
        ConteosCount = state.Conteos.Count;
        ConteosPendientes = state.Conteos.Count(c => c.UploadedAtUtc is null);
        LastSyncText = state.LastSyncUtc is { } last
            ? $"Última sincronización: {last.ToLocalTime():dd/MM/yyyy HH:mm}"
            : "Sin sincronizar";

        if (!string.IsNullOrWhiteSpace(state.LastSyncError))
        {
            SetStatus(state.LastSyncError, true);
        }
        else
        {
            SetStatus(string.Empty, false);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
        HasStatusMessage = !string.IsNullOrWhiteSpace(message);
    }
}
