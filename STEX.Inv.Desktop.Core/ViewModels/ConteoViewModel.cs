using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

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
    private bool _updatingMaterialText;
    private ConteoLinea? _editingLine;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    [NotifyPropertyChangedFor(nameof(CampaignName))]
    private CampanaResumen? _selectedCampaign;

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
        ClearForm();

        var state = await _localStore.LoadAsync();

        WarehouseName = state.Device?.AlmacenDisplay ?? "Almacén";
        OperatorName = _operatorSession.Current?.NombreCompleto ?? "Operario";

        Campanas.Clear();
        foreach (var campana in state.Campanas.OrderBy(c => c.FechaInicio))
        {
            Campanas.Add(campana);
        }

        HasCampaigns = Campanas.Count > 0;
        SelectedCampaign = Campanas.Count == 1 ? Campanas[0] : null;

        _allMaterials.Clear();
        _allMaterials.AddRange(state.Materiales.OrderBy(m => m.Descripcion));

        ApplyMaterialFilter();
        IsSelectingCampaign = true;
        SetStatus(string.Empty, false);
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

        var state = await _localStore.LoadAsync();

        Registros.Clear();
        foreach (var linea in state.Conteos
                     .Where(c => c.CampanaId == SelectedCampaign.Id)
                     .OrderBy(c => c.CreatedAtUtc))
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

    private bool CanContinue() => SelectedCampaign is not null;

    [RelayCommand]
    private void ChangeCampaign()
    {
        ClearForm();
        IsSelectingCampaign = true;
        SetStatus(string.Empty, false);
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
                SetStatus(result.ErrorMessage ?? "No se pudo sincronizar el equipo.", true);
                return;
            }

            await InitializeAsync();
            SetStatus(
                result.UploadedCount switch
                {
                    0 => "Datos maestros actualizados.",
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
            SetStatus("Ingresa un peso neto válido en kilogramos.", true);
            return;
        }

        var operario = _operatorSession.Current;
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

                existing.CampanaId = SelectedCampaign.Id;
                existing.CampanaNombre = SelectedCampaign.Nombre;
                existing.MaterialId = SelectedMaterial.Id;
                existing.MaterialCodigo = SelectedMaterial.CodigoInterno;
                existing.MaterialDescripcion = SelectedMaterial.Descripcion;
                existing.KardexNumero = SelectedMaterial.KardexNumero;
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

            var linea = new ConteoLinea
            {
                CampanaId = SelectedCampaign.Id,
                CampanaNombre = SelectedCampaign.Nombre,
                OperarioId = operario?.Id ?? Guid.Empty,
                OperarioNombre = operario?.NombreCompleto ?? "Operario",
                MaterialId = SelectedMaterial.Id,
                MaterialCodigo = SelectedMaterial.CodigoInterno,
                MaterialDescripcion = SelectedMaterial.Descripcion,
                KardexNumero = SelectedMaterial.KardexNumero,
                Tipo = TipoConteo,
                Unidades = unidades,
                Presentacion = Presentacion,
                PesoNeto = pesoNeto,
                Observacion = observacion
            };

            state.Conteos.Add(linea);
            await _localStore.SaveAsync(state);

            Registros.Add(linea);
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

        IsBusy = true;

        try
        {
            var state = await _localStore.LoadAsync();
            if (state.Conteos.RemoveAll(c => c.Id == linea.Id) > 0)
            {
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

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
               || decimal.TryParse(
                   text.Replace(',', '.'),
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out value);
    }
}
