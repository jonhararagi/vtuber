namespace VtuberBot.Core;

/// <summary>Produces an actionable, non-speculative diagnosis for revoked Twitch EventSub subscriptions.</summary>
public sealed record EventSubRevocationGuidance(string Status, string Message, bool RequiresReauthorization);

public static class EventSubRevocationPolicy
{
    public static EventSubRevocationGuidance Describe(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "authorization_revoked" => new(
                normalized,
                "La autorización OAuth asociada fue revocada o la contraseña de la cuenta cambió; no se intentará volver a registrar esta suscripción con las mismas credenciales.",
                true),
            "user_removed" => new(
                normalized,
                "La cuenta indicada por la condición de la suscripción ya no existe; no se puede restaurar esta suscripción con ese usuario."),
            "version_removed" => new(
                normalized,
                "Twitch retiró esta versión del tipo de evento; hace falta actualizar la definición en VTuberBot antes de registrarla de nuevo."),
            "moderator_removed" => new(
                normalized,
                "La cuenta que había concedido el permiso de moderador ya no es moderadora del canal; restaurá ese rol o volvé a autorizar con una cuenta con permisos."),
            "websocket_disconnected" or "websocket_failed_ping_pong" or "websocket_network_timeout" or "websocket_network_error" =>
                new(normalized,
                    "La suscripción se revocó por una condición de transporte WebSocket. La conexión puede recuperarse, pero esta suscripción ya no se asume activa; reconectá y comprobá el estado de suscripciones."),
            "" => new(
                "unknown",
                "Twitch no proporcionó el motivo de revocación; comprobá el diagnóstico y los permisos antes de volver a crear la suscripción."),
            _ => new(
                normalized,
                "El motivo no se reconoce en esta versión. No se intentará restaurar silenciosamente; comprobá el estado de la suscripción y los permisos requeridos.")
        };
    }
}
