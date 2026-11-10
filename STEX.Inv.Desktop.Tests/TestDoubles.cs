using STEX.Inv.Desktop.Core.DTOs;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Tests;

internal sealed class FakeLocalStore : ILocalStore
{
    public LocalState State { get; set; } = new();

    public Task<LocalState> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(State);

    public Task SaveAsync(LocalState state, CancellationToken cancellationToken = default)
    {
        State = state;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        State = new LocalState();
        return Task.CompletedTask;
    }
}

internal sealed class FakeOperatorSession(Operario? operario) : IOperatorSession
{
    public Operario? Current => operario;
    public bool IsSignedIn => operario is not null;
    public void SignIn(Operario value) { }
    public void SignOut() { }
}

internal sealed class FakeDialogService : IDialogService
{
    public bool Result { get; set; }
    public Guid? SelectionResult { get; set; }
    public int Calls { get; private set; }
    public string LastTitle { get; private set; } = string.Empty;
    public string LastMessage { get; private set; } = string.Empty;
    public IReadOnlyList<SelectionItem> LastSelectionItems { get; private set; } = Array.Empty<SelectionItem>();

    public bool Confirm(string title, string message)
    {
        Calls++;
        LastTitle = title;
        LastMessage = message;
        return Result;
    }

    public Guid? SelectItem(string title, string message, IReadOnlyList<SelectionItem> items)
    {
        Calls++;
        LastTitle = title;
        LastMessage = message;
        LastSelectionItems = items;
        return SelectionResult;
    }
}

internal sealed class FakeFileDialogService : IFileDialogService
{
    public string? Folder { get; set; }
    public string? LastTitle { get; private set; }

    public string? PickFolder(string title, string? initialDirectory = null)
    {
        LastTitle = title;
        return Folder;
    }
}

internal sealed class FakeBookExportService : IBookExportService
{
    public List<BookExportData> ExportedBooks { get; } = new();
    public List<string> Folders { get; } = new();

    public Task<string> ExportBookAsync(
        BookExportData book,
        string warehouseName,
        string folder,
        CancellationToken cancellationToken = default)
    {
        ExportedBooks.Add(book);
        Folders.Add(folder);
        return Task.FromResult(Path.Combine(folder, $"{book.LibroLocalId:N}.xlsx"));
    }
}

internal sealed class FakeWarehouseSyncService : IWarehouseSyncService
{
    public SyncResult Result { get; set; } = new(SyncStatus.Success);
    public IReadOnlyCollection<Guid>? LastBookIds { get; private set; }
    public int Calls { get; private set; }

    public Task<SyncResult> SyncAsync(
        IReadOnlyCollection<Guid>? libroLocalIds = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        LastBookIds = libroLocalIds;
        return Task.FromResult(Result);
    }
}

internal static class TestData
{
    public static CampanaResumen Campaign(
        string name,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string status = "Abierta") =>
        new()
        {
            Id = Guid.NewGuid(),
            Nombre = name,
            Estado = status,
            FechaInicio = start,
            FechaFin = end
        };

    public static Operario Operator(string name = "Operario prueba") =>
        new()
        {
            Id = Guid.NewGuid(),
            NombreCompleto = name,
            Activo = true
        };

    public static LibroLocal Book(
        CampanaResumen campaign,
        Operario operario,
        DateTimeOffset createdAt,
        EstadoLibroLocal state = EstadoLibroLocal.Borrador,
        Guid? remoteId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CampanaId = campaign.Id,
            CampanaNombre = campaign.Nombre,
            OperarioId = operario.Id,
            OperarioNombre = operario.NombreCompleto,
            Estado = state,
            CreadoAtUtc = createdAt,
            LibroRemotoId = remoteId
        };

    public static ConteoLinea Line(
        LibroLocal book,
        CampanaResumen campaign,
        Operario operario,
        DateTimeOffset createdAt,
        string description = "Material",
        Guid? remoteId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            LibroLocalId = book.Id,
            CampanaId = campaign.Id,
            CampanaNombre = campaign.Nombre,
            OperarioId = operario.Id,
            OperarioNombre = operario.NombreCompleto,
            MaterialId = Guid.NewGuid(),
            MaterialCodigo = "COD-1",
            MaterialDescripcion = description,
            Unidad = "KG",
            Tipo = "Hilo",
            Unidades = 1,
            Presentacion = "Bultos",
            PesoNeto = 1m,
            CreatedAtUtc = createdAt,
            LibroId = remoteId,
            UploadedAtUtc = remoteId is null ? null : createdAt
        };
}
