using CommunityToolkit.Mvvm.ComponentModel;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Panel principal dentro del shell.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly ILocalStore _localStore;

    [ObservableProperty]
    private int _operariosCount;

    [ObservableProperty]
    private int _campanasCount;

    [ObservableProperty]
    private int _materialesCount;

    [ObservableProperty]
    private string _ultimaSyncText = "Sin sincronizar";

    [ObservableProperty]
    private string _almacenNombre = "Almacén";

    [ObservableProperty]
    private string _campanaActual = "Sin campaña activa";

    public DashboardViewModel(ILocalStore localStore)
    {
        _localStore = localStore;
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        var state = await _localStore.LoadAsync();

        AlmacenNombre = state.Device?.AlmacenDisplay ?? "Almacén";
        OperariosCount = state.Operarios.Count(o => o.Activo);
        CampanasCount = state.Campanas.Count;
        MaterialesCount = state.MaterialesCount;
        CampanaActual = state.Campanas.FirstOrDefault()?.Nombre ?? "Sin campaña activa";

        UltimaSyncText = state.LastSyncUtc is { } last
            ? $"Última sincronización: {last.ToLocalTime():dd/MM/yyyy HH:mm}"
            : "Sin sincronizar";
    }
}
