using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Libros de conteo guardados localmente, agrupados por campaña.
/// </summary>
public partial class LibrosViewModel : ViewModelBase
{
    private readonly ILocalStore _localStore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLibros))]
    private bool _isEmpty = true;

    [ObservableProperty]
    private int _totalRegistros;

    [ObservableProperty]
    private string _totalPesoText = "0 kg";

    public ObservableCollection<LibroResumen> Libros { get; } = new();

    public bool HasLibros => !IsEmpty;

    public LibrosViewModel(ILocalStore localStore)
    {
        _localStore = localStore;
    }

    public override Task InitializeAsync(object? parameter = null) => LoadAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var state = await _localStore.LoadAsync();

        Libros.Clear();
        foreach (var grupo in state.Conteos
                     .GroupBy(c => c.CampanaId)
                     .OrderByDescending(g => g.Max(c => c.CreatedAtUtc)))
        {
            var primera = grupo.First();
            Libros.Add(new LibroResumen(
                grupo.Key,
                string.IsNullOrWhiteSpace(primera.CampanaNombre) ? "Campaña" : primera.CampanaNombre,
                grupo.Count(),
                grupo.Sum(c => c.Unidades),
                grupo.Sum(c => c.PesoNeto),
                grupo.Select(c => c.OperarioId).Distinct().Count(),
                grupo.Max(c => c.CreatedAtUtc)));
        }

        TotalRegistros = state.Conteos.Count;
        TotalPesoText = $"{state.Conteos.Sum(c => c.PesoNeto):0.##} kg";
        IsEmpty = Libros.Count == 0;
    }
}

public sealed record LibroResumen(
    Guid CampanaId,
    string CampanaNombre,
    int Registros,
    int Unidades,
    decimal PesoNeto,
    int Operarios,
    DateTimeOffset UltimaActualizacion)
{
    public string UltimaActualizacionTexto =>
        UltimaActualizacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string PesoNetoTexto => $"{PesoNeto:0.##} kg";
}
