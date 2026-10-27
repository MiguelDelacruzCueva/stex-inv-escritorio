namespace STEX.Inv.Desktop.Core.ViewModels;

/// <summary>
/// Entrada del menú lateral del shell.
/// </summary>
public sealed record NavModule(string Key, string Title, string Icon);

/// <summary>
/// Parámetro de navegación para módulos aún no implementados.
/// </summary>
public sealed record ModuleRequest(string Title, string Description);
