namespace STEX.Inv.Desktop.Core.Models;

/// <summary>
/// Asigna un libro local a los conteos creados antes de existir LibroLocal.
/// Es idempotente: se puede ejecutar en cada carga sin duplicar libros.
/// </summary>
public static class LocalBookMigration
{
    public static bool EnsureLocalBooks(LocalState state)
    {
        state.Libros ??= new List<LibroLocal>();
        state.Conteos ??= new List<ConteoLinea>();
        state.Materiales ??= new List<Material>();

        var changed = false;
        var booksById = state.Libros.ToDictionary(b => b.Id);

        foreach (var line in state.Conteos.Where(l => string.IsNullOrWhiteSpace(l.Unidad)))
        {
            var material = state.Materiales.FirstOrDefault(m => m.Id == line.MaterialId);
            if (!string.IsNullOrWhiteSpace(material?.Unidad))
            {
                line.Unidad = material.Unidad;
                changed = true;
            }
        }

        foreach (var line in state.Conteos)
        {
            if (line.LibroLocalId != Guid.Empty
                && booksById.TryGetValue(line.LibroLocalId, out var assignedBook))
            {
                if (line.LibroId is { } remoteId
                    && assignedBook.LibroRemotoId != remoteId)
                {
                    assignedBook.LibroRemotoId = remoteId;
                    changed = true;
                }

                continue;
            }

            var book = state.Libros.FirstOrDefault(b =>
                b.CampanaId == line.CampanaId
                && b.OperarioId == line.OperarioId
                && b.LibroRemotoId == line.LibroId);

            if (book is null)
            {
                book = new LibroLocal
                {
                    Id = Guid.NewGuid(),
                    CampanaId = line.CampanaId,
                    CampanaNombre = line.CampanaNombre,
                    OperarioId = line.OperarioId,
                    OperarioNombre = line.OperarioNombre,
                    LibroRemotoId = line.LibroId,
                    CreadoAtUtc = line.CreatedAtUtc,
                    EnviadoAtUtc = line.UploadedAtUtc
                };

                state.Libros.Add(book);
                booksById[book.Id] = book;
                changed = true;
            }

            if (line.LibroLocalId != book.Id)
            {
                line.LibroLocalId = book.Id;
                changed = true;
            }
            else if (line.LibroId is { } lineRemoteId
                     && book.LibroRemotoId != lineRemoteId)
            {
                book.LibroRemotoId = lineRemoteId;
                changed = true;
            }
        }

        foreach (var book in state.Libros)
        {
            var lines = state.Conteos
                .Where(l => l.LibroLocalId == book.Id)
                .ToList();

            if (lines.Count == 0)
            {
                continue;
            }

            var first = lines[0];
            if (book.CampanaId == Guid.Empty)
            {
                book.CampanaId = first.CampanaId;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(book.CampanaNombre))
            {
                book.CampanaNombre = first.CampanaNombre;
                changed = true;
            }
            if (book.OperarioId == Guid.Empty)
            {
                book.OperarioId = first.OperarioId;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(book.OperarioNombre))
            {
                book.OperarioNombre = first.OperarioNombre;
                changed = true;
            }

            var createdAt = lines.Min(l => l.CreatedAtUtc);
            if (createdAt != default && book.CreadoAtUtc != createdAt)
            {
                book.CreadoAtUtc = createdAt;
                changed = true;
            }

            var uploadedAt = lines
                .Where(l => l.UploadedAtUtc is not null)
                .Select(l => l.UploadedAtUtc)
                .Max();
            if (uploadedAt != book.EnviadoAtUtc)
            {
                book.EnviadoAtUtc = uploadedAt;
                changed = true;
            }

            var remoteId = lines
                .Where(l => l.LibroId is not null)
                .Select(l => l.LibroId)
                .FirstOrDefault();
            if (remoteId is not null && book.LibroRemotoId != remoteId)
            {
                book.LibroRemotoId = remoteId;
                changed = true;
            }

            if (book.Estado == EstadoLibroLocal.Reemplazado)
            {
                continue;
            }

            var uploadedCount = lines.Count(l => l.UploadedAtUtc is not null);
            var bookState = uploadedCount switch
            {
                0 => EstadoLibroLocal.Borrador,
                _ when uploadedCount == lines.Count => EstadoLibroLocal.Enviado,
                _ => EstadoLibroLocal.Parcial
            };

            if (book.Estado != bookState)
            {
                book.Estado = bookState;
                changed = true;
            }
        }

        return changed;
    }
}
