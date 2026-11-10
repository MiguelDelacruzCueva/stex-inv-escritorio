using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Core.Validation;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Libros de conteo guardados localmente, agrupados por campaña.
/// </summary>
public partial class LibrosViewModel : ViewModelBase
{
    private const int PageSize = 20;

    private readonly ILocalStore _localStore;
    private readonly IOperatorSession _operatorSession;
    private readonly IFileDialogService _fileDialog;
    private readonly IBookExportService _exportService;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLibros))]
    private bool _isEmpty = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    [NotifyPropertyChangedFor(nameof(HasMultiplePages))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _totalPages = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyBookCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteBookCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResolveDuplicateDraftsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResolveDuplicateDraftsCommand))]
    private bool _hasDuplicateDrafts;

    [ObservableProperty]
    private string _duplicateDraftsMessage = string.Empty;

    [ObservableProperty]
    private int _totalRegistros;

    [ObservableProperty]
    private string _totalPesoText = "0 kg";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    public ObservableCollection<LibroResumen> Libros { get; } = new();

    public ObservableCollection<LibroResumen> PagedLibros { get; } = new();

    public bool HasLibros => !IsEmpty;

    public bool HasMultiplePages => TotalPages > 1;

    public string PageText => $"Página {CurrentPage} de {TotalPages}";

    public LibrosViewModel(
        ILocalStore localStore,
        IOperatorSession operatorSession,
        IFileDialogService fileDialog,
        IBookExportService exportService,
        IDialogService dialogs)
    {
        _localStore = localStore;
        _operatorSession = operatorSession;
        _fileDialog = fileDialog;
        _exportService = exportService;
        _dialogs = dialogs;
    }

    public override Task InitializeAsync(object? parameter = null) => LoadAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousPage() => CurrentPage--;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPage() => CurrentPage++;

    private bool CanGoPrevious() => CurrentPage > 1;

    private bool CanGoNext() => CurrentPage < TotalPages;

    partial void OnCurrentPageChanged(int value) => ApplyPage();

    [RelayCommand]
    private Task ExportSelectedAsync() =>
        ExportBooksAsync(Libros.Where(l => l.IsSelected).ToList());

    [RelayCommand]
    private Task ExportAllAsync() =>
        ExportBooksAsync(Libros.ToList());

    private async Task ExportBooksAsync(IReadOnlyList<LibroResumen> books)
    {
        if (books.Count == 0)
        {
            SetStatus("Selecciona al menos un libro para exportar.", true);
            return;
        }

        try
        {
            var folder = _fileDialog.PickFolder(
                "Selecciona la carpeta donde guardar los Excel",
                DefaultDownloadsFolder());

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            var state = await _localStore.LoadAsync();
            var warehouseName = string.IsNullOrWhiteSpace(state.Device?.AlmacenNombre)
                ? "Almacén"
                : state.Device.AlmacenNombre;
            var exported = 0;

            foreach (var book in books)
            {
                await _exportService.ExportBookAsync(
                    new BookExportData(
                        book.LibroLocalId,
                        book.CampanaNombre,
                        book.OperarioNombre,
                        book.CreadoAtUtc,
                        book.Lineas),
                    warehouseName,
                    folder);

                exported++;
            }

            SetStatus(
                $"Se exportaron {exported} archivo{(exported == 1 ? string.Empty : "s")} en {folder}.",
                false);
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo exportar: {ex.Message}", true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyBook))]
    private async Task CopyBookAsync(LibroResumen? book)
    {
        if (book is null)
        {
            return;
        }

        var operario = _operatorSession.Current;
        if (operario is null)
        {
            SetStatus("No hay un operario activo para hacer la copia.", true);
            return;
        }

        try
        {
            IsBusy = true;
            var state = await _localStore.LoadAsync();
            var sourceBook = state.Libros.FirstOrDefault(b => b.Id == book.LibroLocalId);
            var sourceLines = state.Conteos
                .Where(c => c.LibroLocalId == book.LibroLocalId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToList();

            if (sourceBook is null || sourceLines.Count == 0)
            {
                SetStatus("El libro ya no existe en este equipo.", true);
                return;
            }

            if (sourceBook.Estado == EstadoLibroLocal.Reemplazado)
            {
                SetStatus("Este libro ya fue reemplazado; no se puede copiar.", true);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var destinations = CampaignRules
                .Available(state.Campanas, now)
                .OrderBy(c => c.FechaInicio)
                .Select(campaign =>
                {
                    var hasDraft = BookRules.HasActiveDraft(state, campaign.Id, operario.Id);
                    return new SelectionItem(
                        campaign.Id,
                        campaign.Nombre,
                        hasDraft
                            ? $"{campaign.RangoFechas} · Ya tienes un borrador en esta campaña"
                            : campaign.RangoFechas,
                        !hasDraft);
                })
                .ToList();

            if (destinations.Count == 0)
            {
                SetStatus("No hay campañas abiertas y vigentes para copiar.", true);
                return;
            }

            if (destinations.All(item => !item.IsEnabled))
            {
                SetStatus(
                    "Ya tienes un borrador en todas las campañas abiertas. Elige otra campaña o continúa el borrador existente.",
                    true);
                return;
            }

            var destinationId = _dialogs.SelectItem(
                "Copiar libro",
                "Elige la campaña destino. Las campañas donde ya tienes un borrador aparecen bloqueadas.",
                destinations);

            if (destinationId is null)
            {
                return;
            }

            var destination = state.Campanas.FirstOrDefault(c => c.Id == destinationId.Value);
            if (destination is null)
            {
                SetStatus("La campaña destino ya no está disponible.", true);
                return;
            }

            if (BookRules.HasActiveDraft(state, destination.Id, operario.Id))
            {
                SetStatus("Ya tienes un borrador en esa campaña. Elige otra.", true);
                return;
            }

            var sameCampaign = sourceBook.CampanaId == destination.Id;
            var replaceSource = sourceBook.Estado == EstadoLibroLocal.Parcial
                                || (sameCampaign && sourceBook.Estado == EstadoLibroLocal.Enviado);

            if (replaceSource)
            {
                sourceBook.Estado = EstadoLibroLocal.Reemplazado;
            }

            var newBook = new LibroLocal
            {
                Id = Guid.NewGuid(),
                CampanaId = destination.Id,
                CampanaNombre = destination.Nombre,
                OperarioId = operario.Id,
                OperarioNombre = operario.NombreCompleto,
                Estado = EstadoLibroLocal.Borrador,
                CreadoAtUtc = DateTimeOffset.UtcNow,
                ReemplazaA = replaceSource ? book.LibroLocalId : null
            };

            for (var index = 0; index < sourceLines.Count; index++)
            {
                var source = sourceLines[index];
                state.Conteos.Add(new ConteoLinea
                {
                    Id = Guid.NewGuid(),
                    LibroLocalId = newBook.Id,
                    CampanaId = destination.Id,
                    CampanaNombre = destination.Nombre,
                    OperarioId = operario.Id,
                    OperarioNombre = operario.NombreCompleto,
                    MaterialId = source.MaterialId,
                    MaterialCodigo = source.MaterialCodigo,
                    MaterialDescripcion = source.MaterialDescripcion,
                    KardexNumero = source.KardexNumero,
                    Unidad = source.Unidad,
                    Tipo = source.Tipo,
                    Unidades = source.Unidades,
                    Presentacion = source.Presentacion,
                    PesoNeto = source.PesoNeto,
                    Observacion = source.Observacion,
                    CreatedAtUtc = now.AddTicks(-index)
                });
            }

            state.Libros.Add(newBook);
            await _localStore.SaveAsync(state);
            await LoadAsync();

            SetStatus(
                replaceSource
                    ? $"Libro copiado a {destination.Nombre}. El original quedó como reemplazado."
                    : $"Libro copiado a {destination.Nombre}.",
                false);
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo copiar el libro: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCopyBook(LibroResumen? book) => book is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDeleteBook))]
    private async Task DeleteBookAsync(LibroResumen? book)
    {
        if (book is null)
        {
            return;
        }

        var message =
            $"Libro: {book.CampanaNombre}\n" +
            $"Operario: {book.OperarioNombre}\n" +
            $"Registros: {book.Registros}\n\n" +
            "¿Eliminar este libro de este equipo?";

        if (book.LibroId is not null)
        {
            message += "\n\nYa fue enviado: el libro remoto no se elimina.";
        }

        if (!_dialogs.Confirm("Eliminar libro", message))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var state = await _localStore.LoadAsync();
            state.Libros.RemoveAll(b => b.Id == book.LibroLocalId);
            state.Conteos.RemoveAll(c => c.LibroLocalId == book.LibroLocalId);
            await _localStore.SaveAsync(state);
            await LoadAsync();

            SetStatus("Libro eliminado.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo eliminar el libro: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDeleteBook(LibroResumen? book) => book is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanResolveDuplicateDrafts))]
    private async Task ResolveDuplicateDraftsAsync()
    {
        var operario = _operatorSession.Current;

        try
        {
            IsBusy = true;
            var state = await _localStore.LoadAsync();
            var groups = BookRules.DuplicateDrafts(state, operario?.Id);

            if (groups.Count == 0)
            {
                await LoadAsync();
                SetStatus("No hay borradores duplicados.", false);
                return;
            }

            // Primero se recogen todas las decisiones; si se cancela una,
            // no se guarda ningún cambio parcial.
            var decisions = new List<(Guid KeepId, List<LibroLocal> Books)>();
            foreach (var group in groups)
            {
                var books = group.OrderByDescending(b => b.CreadoAtUtc).ToList();
                var items = books
                    .Select(b => new SelectionItem(
                        b.Id,
                        $"{b.CampanaNombre} · {b.CreadoAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}",
                        $"Operario: {b.OperarioNombre} · " +
                        $"{state.Conteos.Count(c => c.LibroLocalId == b.Id)} registro(s)"))
                    .ToList();

                var keepId = _dialogs.SelectItem(
                    "Resolver duplicados",
                    $"La campaña {books[0].CampanaNombre} tiene {books.Count} borradores. " +
                    "Elige el que se conservará; los demás se marcarán como reemplazados.",
                    items);

                if (keepId is null)
                {
                    SetStatus("Resolución de duplicados cancelada.", true);
                    return;
                }

                if (books.All(b => b.Id != keepId.Value))
                {
                    SetStatus("La selección del duplicado no es válida. Inténtalo de nuevo.", true);
                    return;
                }

                decisions.Add((keepId.Value, books));
            }

            foreach (var decision in decisions)
            {
                foreach (var book in decision.Books.Where(b => b.Id != decision.KeepId))
                {
                    book.Estado = EstadoLibroLocal.Reemplazado;
                }
            }

            await _localStore.SaveAsync(state);
            await LoadAsync();
            SetStatus("Duplicados resueltos.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudieron resolver los duplicados: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanResolveDuplicateDrafts() => HasDuplicateDrafts && !IsBusy;

    private static string DefaultDownloadsFolder()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        return Directory.Exists(downloads)
            ? downloads
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
        HasStatusMessage = !string.IsNullOrWhiteSpace(message);
    }

    private async Task LoadAsync()
    {
        var state = await _localStore.LoadAsync();
        var operarioId = _operatorSession.Current?.Id;
        var libros = state.Libros
            .Where(b => operarioId is null || b.OperarioId == operarioId)
            .OrderByDescending(b => b.CreadoAtUtc)
            .ToList();

        var duplicateGroups = BookRules.DuplicateDrafts(state, operarioId);
        HasDuplicateDrafts = duplicateGroups.Count > 0;
        DuplicateDraftsMessage = duplicateGroups.Count switch
        {
            0 => string.Empty,
            1 => "Hay una campaña con más de un borrador. Elige cuál conservar.",
            _ => $"Hay {duplicateGroups.Count} campañas con más de un borrador. Elige cuál conservar en cada una."
        };

        Libros.Clear();
        foreach (var book in libros)
        {
            var lineas = state.Conteos
                .Where(c => c.LibroLocalId == book.Id)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToList();

            if (lineas.Count == 0)
            {
                continue;
            }

            Libros.Add(new LibroResumen(
                book.Id,
                book.CampanaId,
                book.LibroRemotoId,
                string.IsNullOrWhiteSpace(book.CampanaNombre) ? "Campaña" : book.CampanaNombre,
                string.IsNullOrWhiteSpace(book.OperarioNombre) ? "Operario" : book.OperarioNombre,
                lineas.Count,
                lineas.Sum(c => c.Unidades),
                lineas.Sum(c => c.PesoNeto),
                book.CreadoAtUtc,
                lineas.Max(c => c.CreatedAtUtc),
                lineas.Count(c => c.UploadedAtUtc is null),
                lineas.Count(c => c.UploadedAtUtc is not null),
                book.Estado,
                lineas));
        }

        var visibles = Libros.SelectMany(l => l.Lineas).ToList();
        TotalRegistros = visibles.Count;
        TotalPesoText = $"{visibles.Sum(c => c.PesoNeto):0.##} kg";
        IsEmpty = Libros.Count == 0;

        TotalPages = Math.Max(1, (int)Math.Ceiling(Libros.Count / (double)PageSize));
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);
        ApplyPage();
    }

    private void ApplyPage()
    {
        PagedLibros.Clear();
        foreach (var book in Libros
                     .Skip((CurrentPage - 1) * PageSize)
                     .Take(PageSize))
        {
            PagedLibros.Add(book);
        }
    }
}

public sealed record LibroResumen(
    Guid LibroLocalId,
    Guid CampanaId,
    Guid? LibroId,
    string CampanaNombre,
    string OperarioNombre,
    int Registros,
    int Unidades,
    decimal PesoNeto,
    DateTimeOffset CreadoAtUtc,
    DateTimeOffset UltimaActualizacion,
    int Pendientes,
    int Subidos,
    EstadoLibroLocal Estado,
    IReadOnlyList<ConteoLinea> Lineas)
{
    public bool IsSelected { get; set; }

    public string UltimaActualizacionTexto =>
        UltimaActualizacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string PesoNetoTexto => $"{PesoNeto:0.##} kg";

    public string LibroIdTexto => LibroId is null
        ? "Borrador local"
        : $"Libro {LibroId.Value.ToString("N")[..8]}";

    public bool Sincronizado => Estado == EstadoLibroLocal.Enviado
                                || (Pendientes == 0 && Registros > 0);

    public string EstadoTexto => Estado switch
    {
        EstadoLibroLocal.Reemplazado => "Reemplazado",
        EstadoLibroLocal.Enviado => "Sincronizado",
        EstadoLibroLocal.Parcial => "Parcial",
        _ => Subidos > 0 ? "Parcial" : "Guardado local"
    };
}
