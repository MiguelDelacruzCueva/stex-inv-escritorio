using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

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

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    public ObservableCollection<LibroResumen> Libros { get; } = new();

    public bool HasLibros => !IsEmpty;

    public LibrosViewModel(ILocalStore localStore)
    {
        _localStore = localStore;
    }

    public override Task InitializeAsync(object? parameter = null) => LoadAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            var state = await _localStore.LoadAsync();

            if (state.Conteos.Count == 0)
            {
                SetStatus("No hay registros para exportar.", true);
                return;
            }

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "STEX Inventario");
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"libros_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            await File.WriteAllTextAsync(path, BuildCsv(state.Conteos), new UTF8Encoding(true));

            SetStatus($"Exportado: {path}", false);
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo exportar: {ex.Message}", true);
        }
    }

    private static string BuildCsv(IEnumerable<ConteoLinea> lineas)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Campaña,Fecha,Operario,Material,Código,Kardex,Tipo,Unidades,Presentación,Peso neto (kg),Observación,Subido");

        foreach (var linea in lineas.OrderBy(l => l.CampanaNombre).ThenBy(l => l.CreatedAtUtc))
        {
            csv.AppendLine(string.Join(",", new[]
            {
                Csv(linea.CampanaNombre),
                Csv(linea.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(linea.OperarioNombre),
                Csv(linea.MaterialDescripcion),
                Csv(linea.MaterialCodigo),
                Csv(linea.KardexNumero),
                Csv(linea.Tipo),
                Csv(linea.Unidades.ToString(CultureInfo.InvariantCulture)),
                Csv(linea.Presentacion),
                Csv(linea.PesoNeto.ToString("0.###", CultureInfo.InvariantCulture)),
                Csv(linea.Observacion),
                Csv(linea.UploadedAtUtc is null ? "No" : "Sí")
            }));
        }

        return csv.ToString();
    }

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
        HasStatusMessage = !string.IsNullOrWhiteSpace(message);
    }

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
                grupo.Max(c => c.CreatedAtUtc),
                grupo.Count(c => c.UploadedAtUtc is null),
                grupo.Count(c => c.UploadedAtUtc is not null),
                grupo.OrderBy(c => c.CreatedAtUtc).ToList()));
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
    DateTimeOffset UltimaActualizacion,
    int Pendientes,
    int Subidos,
    IReadOnlyList<ConteoLinea> Lineas)
{
    public string UltimaActualizacionTexto =>
        UltimaActualizacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string PesoNetoTexto => $"{PesoNeto:0.##} kg";

    public bool Sincronizado => Pendientes == 0;

    public string EstadoTexto => Sincronizado
        ? "Sincronizado"
        : Subidos > 0
            ? "Parcial"
            : "Guardado local";
}
