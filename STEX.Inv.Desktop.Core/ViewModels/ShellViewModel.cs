using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using STEX.Inv.Desktop.Core.Interfaces;

namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Contenedor autenticado: sidebar, cabecera y región de módulos.
/// </summary>
public partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly IOperatorSession _operatorSession;
    private readonly IDeviceSessionService _deviceSession;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ViewModelBase? _currentModule;

    [ObservableProperty]
    private NavModule? _selectedModule;

    [ObservableProperty]
    private string _operatorName = string.Empty;

    [ObservableProperty]
    private string _warehouseName = string.Empty;

    public ObservableCollection<NavModule> Modules { get; } = new()
    {
    new("counts", "Conteo", "ClipboardListOutline"),
    new("books", "Libros", "BookOpenVariant"),
    new("sync", "Sync", "CloudSyncOutline"),
    new("profile", "Perfil", "AccountCircleOutline")
    };

    public ShellViewModel(
        IServiceProvider services,
        IOperatorSession operatorSession,
        IDeviceSessionService deviceSession,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _services = services;
        _operatorSession = operatorSession;
        _deviceSession = deviceSession;
        _navigation = navigation;
        _dialogs = dialogs;
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        OperatorName = _operatorSession.Current?.NombreCompleto ?? "Operario";

        var device = await _deviceSession.GetAsync();
        WarehouseName = device?.AlmacenDisplay ?? "Almacén";

        SelectedModule = Modules[0];
    }

    partial void OnSelectedModuleChanged(NavModule? value)
    {
        if (value is null)
        {
            return;
        }

        _ = OpenModuleSafeAsync(value.Key);
    }

    private async Task OpenModuleSafeAsync(string key)
    {
        try
        {
            ViewModelBase? module = key switch
            {
                "counts" => await CreatePlaceholderAsync(
                    "Conteo",
                    "Consulta y registra los conteos físicos del inventario."),

                "books" => await CreatePlaceholderAsync(
                    "Libros",
                    "Consulta los libros y registros asociados al inventario."),

                "sync" => await CreatePlaceholderAsync(
                    "Sincronización",
                    "Sincroniza la información local con el sistema central."),

                "profile" => await CreatePlaceholderAsync(
                    "Perfil",
                    $"Información del operario: {OperatorName}."),

                _ => null
            };

            if (module is null)
            {
                return;
            }

            await module.InitializeAsync();
            CurrentModule = module;
        }
        catch (Exception ex)
        {
            CurrentModule = await CreatePlaceholderAsync("Módulo no disponible", ex.Message);
        }
    }

    private TViewModel Resolve<TViewModel>() where TViewModel : ViewModelBase
    {
        return _services.GetService(typeof(TViewModel)) as TViewModel
               ?? throw new InvalidOperationException(
                   $"El ViewModel '{typeof(TViewModel).Name}' no está registrado.");
    }

    private async Task<ViewModelBase> CreatePlaceholderAsync(string title, string description)
    {
        var viewModel = Resolve<PlaceholderViewModel>();
        await viewModel.InitializeAsync(new ModuleRequest(title, description));
        return viewModel;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _operatorSession.SignOut();
        await _navigation.NavigateToAsync<OperatorSelectionViewModel>();
    }

    [RelayCommand]
    private async Task UnbindAsync()
    {
        var confirmed = _dialogs.Confirm(
            "Desvincular equipo",
            "Se eliminarán la sesión del almacén y los operarios guardados en este equipo. " +
            "¿Continuar?");

        if (!confirmed)
        {
            return;
        }

        _operatorSession.SignOut();
        await _deviceSession.UnbindAsync();
        await _navigation.NavigateToAsync<EnrollmentViewModel>();
    }
}
