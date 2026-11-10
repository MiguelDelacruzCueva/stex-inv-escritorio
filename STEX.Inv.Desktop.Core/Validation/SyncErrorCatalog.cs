namespace STEX.Inv.Desktop.Core.Validation;

/// <summary>
/// Avisos centralizados de sincronización. El código lo devuelve el servidor
/// cuando puede; si no viene, se usa el mensaje recibido.
/// </summary>
public sealed record SyncNotice(string Code, string Title, string Message, string Action)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Action)
        ? $"{Title}: {Message}"
        : $"{Title}: {Message} {Action}";
}

public static class SyncErrorCatalog
{
    public static SyncNotice Resolve(string? code, string? fallbackMessage = null) => code switch
    {
        "SESSION_REQUIRED" or "SESSION_EXPIRED" => new(
            code,
            "Sesión vencida",
            "La sesión del almacén no es válida.",
            "Vuelve a vincular el equipo e inténtalo de nuevo."),

        "ACCOUNT_DISABLED" => new(
            code,
            "Cuenta sin permiso",
            "La cuenta no está habilitada para subir conteos.",
            "Contacta al administrador."),

        "PROFILE_CHECK_FAILED" => new(
            code,
            "Cuenta no verificada",
            "No se pudo validar el perfil de la cuenta.",
            "Reintenta; si persiste, vuelve a vincular el equipo."),

        "WAREHOUSE_INACTIVE" => new(
            code,
            "Almacén sin permiso",
            "El almacén no está habilitado para enviar conteos.",
            "Contacta al administrador."),

        "WAREHOUSE_MISSING" or "WAREHOUSE_MISMATCH" or "WAREHOUSE_INVALID" => new(
            code,
            "Almacén incorrecto",
            "El almacén del libro no coincide con la cuenta del equipo.",
            "Vuelve a vincular el equipo."),

        "WAREHOUSE_CHECK_FAILED" => new(
            code,
            "Almacén no verificado",
            "No se pudo validar el almacén.",
            "Reintenta en unos minutos."),

        "CAMPAIGN_NOT_FOUND" or "CAMPAIGN_INVALID" => new(
            code,
            "Campaña no disponible",
            "La campaña ya no existe o no está disponible para este equipo.",
            "Actualiza los datos y elige otra campaña."),

        "CAMPAIGN_CHECK_FAILED" => new(
            code,
            "Campaña no verificada",
            "No se pudo validar la campaña.",
            "Reintenta en unos minutos."),

        "CAMPAIGN_CLOSED" => new(
            code,
            "Campaña cerrada",
            "La campaña ya no admite envíos porque está cerrada.",
            "Elige una campaña abierta."),

        "CAMPAIGN_NOT_STARTED" => new(
            code,
            "Campaña pendiente",
            "La campaña todavía no ha comenzado.",
            "Espera a la fecha de inicio o elige otra campaña."),

        "CAMPAIGN_FINISHED" => new(
            code,
            "Campaña finalizada",
            "La campaña ya terminó y no admite envíos.",
            "Elige otra campaña."),

        "OPERATOR_NOT_ALLOWED" or "OPERATOR_INVALID" => new(
            code,
            "Operario sin permiso",
            "El operario no pertenece al almacén o no está activo.",
            "Contacta al administrador."),

        "OPERATOR_CHECK_FAILED" => new(
            code,
            "Operario no verificado",
            "No se pudo validar el operario.",
            "Reintenta en unos minutos."),

        "BOOK_EMPTY" => new(
            code,
            "Libro vacío",
            "El libro no tiene registros para enviar.",
            "Agrega al menos un registro."),

        "MATERIAL_INVALID" => new(
            code,
            "Material no disponible",
            "Uno de los materiales ya no está disponible en el catálogo.",
            "Actualiza los datos maestros y corrige el registro."),

        "PRESENTATION_INVALID" => new(
            code,
            "Presentación inválida",
            "Una de las presentaciones no es válida.",
            "Corrige el registro antes de enviar."),

        "QUANTITY_INVALID" => new(
            code,
            "Cantidad inválida",
            "Una de las cantidades no es válida.",
            "Corrige el registro antes de enviar."),

        "WEIGHT_INVALID" => new(
            code,
            "Peso inválido",
            "Uno de los pesos no es válido.",
            "Corrige el registro antes de enviar."),

        "BOOK_ID_IN_USE" => new(
            code,
            "Libro ya registrado",
            "El libro ya existe en el servidor.",
            "Crea una copia para corregirlo."),

        "BOOK_ALREADY_SENT" or "BOOK_REPLACED" => new(
            code,
            "Libro ya enviado",
            "El libro ya fue enviado o reemplazado por una copia.",
            "Crea una copia para corregirlo."),

        "BOOK_NOT_FOUND" => new(
            code,
            "Libro no encontrado",
            "No se encontró el libro local de uno de los registros.",
            "Vuelve a abrir el libro desde la lista."),

        "BOOK_ID_INVALID" or "DETAIL_INVALID" => new(
            code,
            "Datos del libro inválidos",
            "El libro tiene un identificador o un detalle inválido.",
            "Corrige el registro antes de enviar."),

        "BOOK_CHECK_FAILED" or "BOOK_CREATE_FAILED" or "DETAILS_CREATE_FAILED" => new(
            code,
            "No se pudo guardar el libro",
            "El servidor no pudo registrar el libro o sus movimientos.",
            "Reintenta; si persiste, contacta al administrador."),

        "NETWORK_ERROR" => new(
            code,
            "Sin conexión",
            "No se pudo conectar con el servidor.",
            "Revisa la conexión y reintenta; los conteos siguen guardados."),

        "TIMEOUT" => new(
            code,
            "Tiempo agotado",
            "La sincronización tardó demasiado.",
            "Reintenta; los conteos siguen guardados."),

        "SERVER_ERROR" => new(
            code,
            "Error del servidor",
            "El servidor no pudo completar la operación.",
            "Reintenta; si persiste, contacta al administrador."),

        _ => new(
            code ?? "SYNC_ERROR",
            "No se pudo sincronizar",
            fallbackMessage ?? "El servidor no pudo completar la operación.",
            "Revisa los datos e inténtalo de nuevo.")
    };
}
