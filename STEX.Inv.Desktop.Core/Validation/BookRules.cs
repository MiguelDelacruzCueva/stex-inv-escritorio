using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Core.Validation;

/// <summary>
/// Reglas de los libros locales: un solo borrador por campaña y operario.
/// </summary>
public static class BookRules
{
    public static bool HasActiveDraft(
        LocalState state,
        Guid campanaId,
        Guid operarioId,
        Guid? exceptBookId = null) =>
        state.Libros.Any(b =>
            b.Id != exceptBookId
            && b.CampanaId == campanaId
            && b.OperarioId == operarioId
            && b.Estado == EstadoLibroLocal.Borrador);

    public static IReadOnlyList<IGrouping<(Guid CampanaId, Guid OperarioId), LibroLocal>>
        DuplicateDrafts(LocalState state, Guid? operarioId = null) =>
        state.Libros
            .Where(b => b.Estado == EstadoLibroLocal.Borrador)
            .Where(b => operarioId is null || b.OperarioId == operarioId)
            .Where(b => state.Conteos.Any(c => c.LibroLocalId == b.Id))
            .GroupBy(b => (b.CampanaId, b.OperarioId))
            .Where(group => group.Count() > 1)
            .ToList();
}
