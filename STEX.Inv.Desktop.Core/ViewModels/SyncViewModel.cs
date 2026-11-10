using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Core.Validation;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Selección de libros pendientes, subida explícita y descarga de maestros.
/// </summary>
public partial class SyncViewModel : ViewModelBase
{
    private readonly ILocalStore _localStore;
    private readonly IWarehouseSyncService _syncService;
    private readonly IOperatorSession _operatorSession;
    private readonly IDialogService _dialogs;

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
    [NotifyCanExecuteChangedFor(nameof(UploadSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UploadAllCommand))]
    private bool _hasPendingBooks;

    [ObservableProperty]
    private bool _hasDuplicateDrafts;

    [ObservableProperty]
    private string _duplicateDraftsMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    [NotifyCanExecuteChangedFor(nameof(UploadSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UploadAllCommand))]
    private bool _isBusy;

    public ObservableCollection<PendingBookItem> PendingBooks { get; } = new();

    public SyncViewModel(
        ILocalStore localStore,
        IWarehouseSyncService syncService,
        IOperatorSession operatorSession,
        IDialogService dialogs)
    {
        _localStore = localStore;
        _syncService = syncService;
        _operatorSession = operatorSession;
        _dialogs = dialogs;
    }

    public override Task InitializeAsync(object? parameter = null) => LoadAsync();

    /// <summary>Solo actualiza catálogos; no sube libros.</summary>
    [RelayCommand(CanExecute = nameof(CanSync))]
    private Task SyncAsync() => RunSyncAsync(Array.Empty<Guid>());

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadSelectedAsync()
    {
        var selected = PendingBooks
            .Where(b => b.IsSelected && b.PuedeSubir)
            .Select(b => b.LibroLocalId)
            .ToList();

        if (selected.Count == 0)
        {
            SetStatus("Selecciona al menos un libro para subir.", true);
            return;
        }

        await RunSyncAsync(selected);
    }

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAllAsync()
    {
        var selected = PendingBooks.Where(b => b.PuedeSubir).ToList();
        if (selected.Count == 0)
        {
            SetStatus("No hay libros disponibles para subir.", true);
            return;
        }

        var campaigns = string.Join(
            ", ",
            selected.Select(b => b.CampanaNombre).Distinct(StringComparer.CurrentCultureIgnoreCase));

        var confirmed = _dialogs.Confirm(
            "Subir todos los libros",
            $"Se subirán {selected.Count} libro(s) de: {campaigns}.\n\n" +
            "Los libros bloqueados permanecerán como borradores. ¿Continuar?");

        if (!confirmed)
        {
            return;
        }

        await RunSyncAsync(selected.Select(b => b.LibroLocalId).ToList());
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var book in PendingBooks.Where(b => b.PuedeSubir))
        {
            book.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var book in PendingBooks)
        {
            book.IsSelected = false;
        }
    }

    private bool CanSync() => !IsBusy;

    private bool CanUpload() => !IsBusy && HasPendingBooks;

    private async Task RunSyncAsync(IReadOnlyCollection<Guid> libroLocalIds)
    {
        IsBusy = true;

        try
        {
            var result = await _syncService.SyncAsync(libroLocalIds);

            if (!result.IsSuccess)
            {
                var message = SyncErrorCatalog
                    .Resolve(result.Code, result.ErrorMessage)
                    .DisplayText;
                await LoadAsync();
                SetStatus(message, true);
                return;
            }

            await LoadAsync();
            SetStatus(
                result.UploadedBooks > 0
                    ? $"Se subieron {result.UploadedBooks} libro(s) y {result.UploadedCount} registro(s). " +
                      "Datos maestros actualizados."
                    : "Datos maestros actualizados correctamente.",
                false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync()
    {
        var state = await _localStore.LoadAsync();
        var operarioId = _operatorSession.Current?.Id;
        var now = DateTimeOffset.UtcNow;

        WarehouseName = state.Device?.AlmacenDisplay ?? "Almacén";
        MaterialesCount = state.Materiales.Count;
        OperariosCount = state.Operarios.Count(o => o.Activo);
        CampanasCount = state.Campanas.Count;
        ConteosCount = state.Conteos.Count;
        LastSyncText = state.LastSyncUtc is { } last
            ? $"Última sincronización: {last.ToLocalTime():dd/MM/yyyy HH:mm}"
            : "Sin sincronizar";

        var duplicateGroups = BookRules.DuplicateDrafts(state, operarioId);
        HasDuplicateDrafts = duplicateGroups.Count > 0;
        DuplicateDraftsMessage = duplicateGroups.Count switch
        {
            0 => string.Empty,
            1 => "Hay una campaña con más de un borrador. Resuélvelo en Libros antes de subir.",
            _ => $"Hay {duplicateGroups.Count} campañas con más de un borrador. Resuélvelos en Libros antes de subir."
        };

        var duplicateIds = duplicateGroups
            .SelectMany(group => group)
            .Select(book => book.Id)
            .ToHashSet();

        PendingBooks.Clear();

        var books = state.Libros
            .Where(b => operarioId is null || b.OperarioId == operarioId)
            .Where(b => b.Estado is EstadoLibroLocal.Borrador or EstadoLibroLocal.Parcial)
            .OrderBy(b => b.CampanaNombre)
            .ThenBy(b => b.CreadoAtUtc)
            .ToList();

        foreach (var book in books)
        {
            var lines = state.Conteos
                .Where(c => c.LibroLocalId == book.Id)
                .ToList();
            var pending = lines.Count(c => c.UploadedAtUtc is null);
            if (pending == 0)
            {
                continue;
            }

            var campaign = state.Campanas.FirstOrDefault(c => c.Id == book.CampanaId);
            var worker = state.Operarios.FirstOrDefault(o => o.Id == book.OperarioId);
            var hasUploadedLines = lines.Any(c => c.UploadedAtUtc is not null);

            var (canUpload, statusText, blockedReason) = Classify(
                book,
                campaign,
                worker,
                now,
                duplicateIds,
                hasUploadedLines);

            PendingBooks.Add(new PendingBookItem
            {
                LibroLocalId = book.Id,
                CampanaNombre = string.IsNullOrWhiteSpace(book.CampanaNombre)
                    ? campaign?.Nombre ?? "Campaña"
                    : book.CampanaNombre,
                OperarioNombre = string.IsNullOrWhiteSpace(book.OperarioNombre)
                    ? worker?.NombreCompleto ?? "Operario"
                    : book.OperarioNombre,
                Registros = lines.Count,
                Pendientes = pending,
                EstadoTexto = statusText,
                PuedeSubir = canUpload,
                Bloqueo = blockedReason
            });
        }

        HasPendingBooks = PendingBooks.Count > 0;
        ConteosPendientes = PendingBooks.Sum(b => b.Pendientes);

        if (!string.IsNullOrWhiteSpace(state.LastSyncError))
        {
            SetStatus(
                SyncErrorCatalog.Resolve(state.LastSyncErrorCode, state.LastSyncError).DisplayText,
                true);
        }
        else
        {
            SetStatus(string.Empty, false);
        }
    }

    private static (bool CanUpload, string StatusText, string? BlockedReason) Classify(
        LibroLocal book,
        CampanaResumen? campaign,
        Operario? worker,
        DateTimeOffset now,
        HashSet<Guid> duplicateIds,
        bool hasUploadedLines)
    {
        if (book.Estado == EstadoLibroLocal.Parcial || hasUploadedLines)
        {
            return (false, "Parcial", "Tiene registros ya subidos. Crea una copia para corregirlo.");
        }

        if (duplicateIds.Contains(book.Id))
        {
            return (false, "Duplicado", "Hay más de un borrador en esta campaña. Resuélvelo en Libros.");
        }

        if (campaign is null)
        {
            return (false, "Bloqueado", "La campaña ya no está en este equipo.");
        }

        if (!CampaignRules.IsOpenAndInRange(campaign, now))
        {
            return (false, "Bloqueado", "La campaña está cerrada o fuera de vigencia.");
        }

        if (worker is null || !worker.Activo)
        {
            return (false, "Bloqueado", "El operario no está activo.");
        }

        return (true, "Listo", null);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
        HasStatusMessage = !string.IsNullOrWhiteSpace(message);
    }
}

public sealed partial class PendingBookItem : ObservableObject
{
    public Guid LibroLocalId { get; init; }

    public string CampanaNombre { get; init; } = string.Empty;

    public string OperarioNombre { get; init; } = string.Empty;

    public int Registros { get; init; }

    public int Pendientes { get; init; }

    public string EstadoTexto { get; init; } = string.Empty;

    public bool PuedeSubir { get; init; }

    public string? Bloqueo { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    public string DetalleTexto =>
        $"{Registros} registro{(Registros == 1 ? string.Empty : "s")} · " +
        $"{Pendientes} pendiente{(Pendientes == 1 ? string.Empty : "s")}";
}
