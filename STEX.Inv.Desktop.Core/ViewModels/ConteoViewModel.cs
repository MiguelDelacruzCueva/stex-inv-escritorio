using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;
using STEX.Inv.Desktop.Core.Validation;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Captura de conteo: primero se elige la campaña y después se registran materiales.
/// </summary>
public partial class ConteoViewModel : ViewModelBase
{
    private const string SinObservacion = "Sin observación";
    private const string DefaultPresentacion = "Bultos";

    private readonly ILocalStore _localStore;
    private readonly IOperatorSession _operatorSession;
    private readonly IWarehouseSyncService _syncService;

    private readonly List<Material> _allMaterials = new();
    private readonly HashSet<Guid> _draftCampaignIds = new();
    private bool _updatingMaterialText;
    private ConteoLinea? _editingLine;
    private Guid? _activeLibroLocalId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewBookCommand))]
    [NotifyPropertyChangedFor(nameof(CampaignName))]
    private CampanaResumen? _selectedCampaign;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewBookCommand))]
    private bool _hasDraftForCampaign;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    private bool _isSelectingCampaign = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCampaigns))]
    private bool _hasCampaigns;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRegistros))]
    private bool _hasRegistros;

    [ObservableProperty]
    private string _warehouseName = "Almacén";

    [ObservableProperty]
    private string _operatorName = "Operario";

    [ObservableProperty]
    private string _tipoConteo = "Hilo";

    [ObservableProperty]
    private string _materialSearchText = string.Empty;

    [ObservableProperty]
    private Material? _selectedMaterial;

    [ObservableProperty]
    private bool _isMaterialDropDownOpen;

    [ObservableProperty]
    private string _kardex = string.Empty;

    [ObservableProperty]
    private string _unidadesText = string.Empty;

    [ObservableProperty]
    private string _presentacion = DefaultPresentacion;

    [ObservableProperty]
    private string _pesoNetoText = string.Empty;

    [ObservableProperty]
    private string _observacion = "CRUDO-APJL";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(AddButtonText))]
    private bool _isEditing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private string _summary = "0 registros";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewBookCommand))]
    private bool _isBusy;

    public ObservableCollection<CampanaResumen> Campanas { get; } = new();

    public ObservableCollection<Material> FilteredMaterials { get; } = new();

    public ObservableCollection<ConteoLinea> Registros { get; } = new();

    public IReadOnlyList<string> TiposConteo { get; } = new[] { "Hilo", "Crudo" };

    public IReadOnlyList<string> TiposPresentacion { get; } =
        new[] { DefaultPresentacion, "Cajas", "Rollos", "Bolsas", "Conos" };

    public IReadOnlyList<string> Observaciones { get; } =
        new[] { SinObservacion, "CRUDO-APJL", "SALDOS", "Devolución" };

    public string CampaignName => SelectedCampaign?.Nombre ?? "Sin campaña";

    public string FormTitle => IsEditing ? "Editar registro" : "Nuevo registro";

    public string AddButtonText => IsEditing ? "Guardar cambios" : "Agregar conteo";

    public bool IsHilo
    {
        get => TipoConteo == "Hilo";
        set
        {
            if (value)
            {
                TipoConteo = "Hilo";
            }
        }
    }

    public bool IsCrudo
    {
        get => TipoConteo == "Crudo";
        set
        {
            if (value)
            {
                TipoConteo = "Crudo";
            }
        }
    }

    public bool IsFormVisible => !IsSelectingCampaign;

    public bool HasNoCampaigns => !HasCampaigns;

    public bool HasNoRegistros => !HasRegistros;

    public ConteoViewModel(
        ILocalStore localStore,
        IOperatorSession operatorSession,
        IWarehouseSyncService syncService)
    {
        _localStore = localStore;
        _operatorSession = operatorSession;
        _syncService = syncService;
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        _activeLibroLocalId = null;
        ClearForm();

        var state = await _localStore.LoadAsync();
        var operario = _operatorSession.Current;

        WarehouseName = state.Device?.AlmacenDisplay ?? "Almacén";
        OperatorName = operario?.NombreCompleto ?? "Operario";

        _draftCampaignIds.Clear();
        if (operario is not null)
        {
            foreach (var draft in state.Libros.Where(b =>
                         b.OperarioId == operario.Id
                         && b.Estado == EstadoLibroLocal.Borrador))
            {
                _draftCampaignIds.Add(draft.CampanaId);
            }
        }

        Campanas.Clear();
        foreach (var campana in CampaignRules
                     .Available(state.Campanas, DateTimeOffset.UtcNow)
                     .OrderBy(c => c.FechaInicio))
        {
            Campanas.Add(campana);
        }

        HasCampaigns = Campanas.Count > 0;
        SelectedCampaign = Campanas.Count == 1 ? Campanas[0] : null;
        HasDraftForCampaign = SelectedCampaign is not null
                              && _draftCampaignIds.Contains(SelectedCampaign.Id);

        _allMaterials.Clear();
        _allMaterials.AddRange(state.Materiales.OrderBy(m => m.Descripcion));

        ApplyMaterialFilter();
        IsSelectingCampaign = true;
        SetStatus(string.Empty, false);
    }

    partial void OnSelectedCampaignChanged(CampanaResumen? value)
    {
        HasDraftForCampaign = value is not null
                              && _draftCampaignIds.Contains(value.Id);
    }

    partial void OnTipoConteoChanged(string value)
    {
        OnPropertyChanged(nameof(IsHilo));
        OnPropertyChanged(nameof(IsCrudo));

        SelectedMaterial = null;
        _updatingMaterialText = true;
        MaterialSearchText = string.Empty;
        _updatingMaterialText = false;
        ApplyMaterialFilter();
        IsMaterialDropDownOpen = false;
    }

    partial void OnMaterialSearchTextChanged(string value)
    {
        if (_updatingMaterialText)
        {
            return;
        }

        if (SelectedMaterial is not null
            && !string.Equals(SelectedMaterial.Display, value, StringComparison.Ordinal))
        {
            SelectedMaterial = null;
        }

        ApplyMaterialFilter();
        IsMaterialDropDownOpen = SelectedMaterial is null
                                 && FilteredMaterials.Count > 0
                                 && !string.IsNullOrWhiteSpace(value);
    }

    partial void OnSelectedMaterialChanged(Material? value)
    {
        if (value is null)
        {
            Kardex = string.Empty;
            return;
        }

        Kardex = value.KardexNumero ?? string.Empty;
        _updatingMaterialText = true;
        MaterialSearchText = value.Display;
        _updatingMaterialText = false;
        IsMaterialDropDownOpen = false;
    }

    private void ApplyMaterialFilter()
    {
        var term = MaterialSearchText?.Trim() ?? string.Empty;
        IEnumerable<Material> query = _allMaterials;

        // Si el catálogo trae tipos, se limita al tipo elegido. Los materiales
        // sin tipo se mantienen visibles para no ocultar datos incompletos.
        if (_allMaterials.Any(m => !string.IsNullOrWhiteSpace(m.Tipo)))
        {
            query = query.Where(m =>
                string.Equals(m.Tipo, TipoConteo, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(m.Tipo));
        }

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(m =>
                m.Display.Contains(term, StringComparison.OrdinalIgnoreCase)
                || m.CodigoInterno.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (m.KardexNumero?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || m.Descripcion.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        FilteredMaterials.Clear();
        foreach (var material in query.OrderBy(m => m.Descripcion))
        {
            FilteredMaterials.Add(material);
        }
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        if (SelectedCampaign is null)
        {
            return;
        }

        var operario = _operatorSession.Current;
        if (operario is null)
        {
            SetStatus("No hay un operario autenticado. Vuelve a ingresar tu PIN.", true);
            return;
        }

        var state = await _localStore.LoadAsync();
        var book = state.Libros
            .Where(b => b.CampanaId == SelectedCampaign.Id
                        && b.OperarioId == operario.Id
                        && b.Estado == EstadoLibroLocal.Borrador)
            .OrderByDescending(b => b.CreadoAtUtc)
            .FirstOrDefault();

        if (book is null)
        {
            SetStatus("No tienes un libro en borrador para esta campaña. Usa “Comenzar uno nuevo”.", true);
            return;
        }

        _activeLibroLocalId = book.Id;
        Registros.Clear();
        foreach (var linea in state.Conteos
                     .Where(c => c.LibroLocalId == book.Id)
                     .OrderByDescending(c => c.CreatedAtUtc))
        {
            Registros.Add(linea);
        }

        UpdateSummary();
        IsSelectingCampaign = false;

        if (_allMaterials.Count == 0)
        {
            SetStatus("No hay materiales guardados. Sincroniza el equipo para descargarlos.", true);
        }
        else
        {
            SetStatus(string.Empty, false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartNewBook))]
    private void NewBook()
    {
        if (SelectedCampaign is null)
        {
            return;
        }

        _activeLibroLocalId = null;
        Registros.Clear();
        ClearForm();
        UpdateSummary();
        IsSelectingCampaign = false;
        SetStatus("Nuevo libro. Se creará al guardar el primer registro.", false);
    }

    private bool CanContinue() =>
        SelectedCampaign is not null && HasDraftForCampaign && !IsBusy;

    private bool CanStartNewBook() =>
        SelectedCampaign is not null && !HasDraftForCampaign && !IsBusy;

    [RelayCommand]
    private void ChangeCampaign()
    {
        _activeLibroLocalId = null;
        ClearForm();
        Registros.Clear();
        UpdateSummary();
        IsSelectingCampaign = true;
        SetStatus(string.Empty, false);
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAsync()
    {
        IsBusy = true;

        try
        {
            // Solo actualiza catálogos: la subida de libros se hace desde Sincronización.
            var result = await _syncService.SyncAsync(Array.Empty<Guid>());

            if (!result.IsSuccess)
            {
                SetStatus(
                    SyncErrorCatalog.Resolve(result.Code, result.ErrorMessage).DisplayText,
                    true);
                return;
            }

            await InitializeAsync();
            SetStatus("Datos maestros actualizados.", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSync() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        if (SelectedCampaign is null || SelectedMaterial is null)
        {
            SetStatus("Selecciona un material para continuar.", true);
            return;
        }

        if (!int.TryParse(UnidadesText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var unidades)
            || unidades <= 0)
        {
            SetStatus("Ingresa una cantidad de unidades mayor que cero.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(Presentacion))
        {
            SetStatus("Selecciona una presentación.", true);
            return;
        }

        if (!TryParseDecimal(PesoNetoText, out var pesoNeto) || pesoNeto < 0)
        {
            SetStatus(
                "Peso neto inválido. Usa coma o punto para decimales (ej. 1,5) y no uses separador de miles.",
                true);
            return;
        }

        var operario = _operatorSession.Current;
        if (operario is null)
        {
            SetStatus("No hay un operario autenticado. Vuelve a ingresar tu PIN.", true);
            return;
        }

        var observacion = NormalizeObservacion(Observacion);
        var editing = _editingLine;

        IsBusy = true;

        try
        {
            var state = await _localStore.LoadAsync();

            if (editing is not null)
            {
                var existing = state.Conteos.FirstOrDefault(c => c.Id == editing.Id);

                if (existing is null)
                {
                    SetStatus("El registro que intentas editar ya no existe.", true);
                    return;
                }

                if (existing.UploadedAtUtc is not null || existing.LibroId is not null)
                {
                    SetStatus("Este registro ya inició su subida y no se puede editar.", true);
                    return;
                }

                var existingBook = state.Libros.FirstOrDefault(b => b.Id == existing.LibroLocalId);
                if (existingBook is null || existingBook.Estado != EstadoLibroLocal.Borrador)
                {
                    SetStatus("El libro de este registro ya no está en borrador.", true);
                    return;
                }

                existing.CampanaId = SelectedCampaign.Id;
                existing.CampanaNombre = SelectedCampaign.Nombre;
                existing.MaterialId = SelectedMaterial.Id;
                existing.MaterialCodigo = SelectedMaterial.CodigoInterno;
                existing.MaterialDescripcion = SelectedMaterial.Descripcion;
                existing.KardexNumero = SelectedMaterial.KardexNumero;
                existing.Unidad = SelectedMaterial.Unidad;
                existing.Tipo = TipoConteo;
                existing.Unidades = unidades;
                existing.Presentacion = Presentacion;
                existing.PesoNeto = pesoNeto;
                existing.Observacion = observacion;

                await _localStore.SaveAsync(state);

                var index = Registros.IndexOf(editing);
                if (index >= 0)
                {
                    Registros[index] = existing;
                }
                else
                {
                    Registros.Add(existing);
                }

                UpdateSummary();
                ClearForm();
                SetStatus("Conteo actualizado correctamente.", false);
                return;
            }

            var book = state.Libros.FirstOrDefault(b => b.Id == _activeLibroLocalId);
            if (book is null)
            {
                if (_activeLibroLocalId is not null)
                {
                    SetStatus("El libro local ya no existe. Vuelve a seleccionar la campaña.", true);
                    return;
                }

                book = new LibroLocal
                {
                    CampanaId = SelectedCampaign.Id,
                    CampanaNombre = SelectedCampaign.Nombre,
                    OperarioId = operario.Id,
                    OperarioNombre = operario.NombreCompleto,
                    CreadoAtUtc = DateTimeOffset.UtcNow
                };
                state.Libros.Add(book);
                _activeLibroLocalId = book.Id;
                _draftCampaignIds.Add(book.CampanaId);
                HasDraftForCampaign = true;
            }

            if (book.Estado != EstadoLibroLocal.Borrador
                || book.CampanaId != SelectedCampaign.Id
                || book.OperarioId != operario.Id)
            {
                SetStatus("El libro activo no está disponible para este operario y campaña.", true);
                return;
            }

            var linea = new ConteoLinea
            {
                LibroLocalId = book.Id,
                CampanaId = SelectedCampaign.Id,
                CampanaNombre = SelectedCampaign.Nombre,
                OperarioId = operario.Id,
                OperarioNombre = operario.NombreCompleto,
                MaterialId = SelectedMaterial.Id,
                MaterialCodigo = SelectedMaterial.CodigoInterno,
                MaterialDescripcion = SelectedMaterial.Descripcion,
                KardexNumero = SelectedMaterial.KardexNumero,
                Unidad = SelectedMaterial.Unidad,
                Tipo = TipoConteo,
                Unidades = unidades,
                Presentacion = Presentacion,
                PesoNeto = pesoNeto,
                Observacion = observacion
            };

            state.Conteos.Add(linea);
            await _localStore.SaveAsync(state);

            Registros.Insert(0, linea);
            UpdateSummary();
            ClearForm();
            SetStatus("Conteo agregado correctamente.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo guardar el conteo: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit(ConteoLinea? linea)
    {
        if (linea is null)
        {
            return;
        }

        if (linea.UploadedAtUtc is not null || linea.LibroId is not null)
        {
            SetStatus("Este registro ya inició su subida y no se puede editar.", true);
            return;
        }

        if (SelectedCampaign is null || linea.CampanaId != SelectedCampaign.Id)
        {
            return;
        }

        TipoConteo = string.Equals(linea.Tipo, "Crudo", StringComparison.OrdinalIgnoreCase)
            ? "Crudo"
            : "Hilo";
        SelectedMaterial = _allMaterials.FirstOrDefault(m => m.Id == linea.MaterialId);
        UnidadesText = linea.Unidades.ToString(CultureInfo.CurrentCulture);
        Presentacion = TiposPresentacion.Contains(linea.Presentacion)
            ? linea.Presentacion
            : DefaultPresentacion;
        PesoNetoText = linea.PesoNeto.ToString("0.###", CultureInfo.CurrentCulture);
        Observacion = string.IsNullOrWhiteSpace(linea.Observacion)
            ? SinObservacion
            : linea.Observacion;

        _editingLine = linea;
        IsEditing = true;
        SetStatus("Editando registro. Guarda los cambios o limpia el formulario.", false);
    }

    private bool CanAdd() => !IsBusy;

    [RelayCommand]
    private async Task RemoveLineAsync(ConteoLinea? linea)
    {
        if (linea is null)
        {
            return;
        }

        if (linea.UploadedAtUtc is not null || linea.LibroId is not null)
        {
            SetStatus("Este registro ya inició su subida y no se puede eliminar.", true);
            return;
        }

        IsBusy = true;

        try
        {
            var state = await _localStore.LoadAsync();
            if (state.Conteos.RemoveAll(c => c.Id == linea.Id) > 0)
            {
                if (linea.LibroLocalId != Guid.Empty
                    && !state.Conteos.Any(c => c.LibroLocalId == linea.LibroLocalId))
                {
                    state.Libros.RemoveAll(b => b.Id == linea.LibroLocalId);
                    if (_activeLibroLocalId == linea.LibroLocalId)
                    {
                        _activeLibroLocalId = null;
                    }
                }

                await _localStore.SaveAsync(state);
            }

            Registros.Remove(linea);
            if (_editingLine?.Id == linea.Id)
            {
                ClearForm();
            }

            UpdateSummary();
            SetStatus("Registro eliminado.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo eliminar el registro: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        _editingLine = null;
        IsEditing = false;
        SelectedMaterial = null;
        _updatingMaterialText = true;
        MaterialSearchText = string.Empty;
        _updatingMaterialText = false;
        ApplyMaterialFilter();
        Kardex = string.Empty;
        UnidadesText = string.Empty;
        Presentacion = DefaultPresentacion;
        PesoNetoText = string.Empty;
        IsMaterialDropDownOpen = false;
    }

    private void UpdateSummary()
    {
        var count = Registros.Count;
        var unidades = Registros.Sum(r => r.Unidades);
        var peso = Registros.Sum(r => r.PesoNeto);

        Summary = $"{count} registro{(count == 1 ? string.Empty : "s")} · " +
                  $"{unidades} unidades · {peso:0.##} kg";
        HasRegistros = count > 0;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
        HasStatusMessage = !string.IsNullOrWhiteSpace(message);
    }

    private static string NormalizeObservacion(string? observacion)
    {
        if (string.IsNullOrWhiteSpace(observacion)
            || string.Equals(observacion, SinObservacion, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return observacion.Trim();
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();

        var lastComma = normalized.LastIndexOf(',');
        var lastDot = normalized.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? '.' : ',';
            normalized = normalized.Replace(groupSeparator.ToString(), string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }
}
