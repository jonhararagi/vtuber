namespace VtuberBot.Core;

/// <summary>Produces an actionable, non-speculative diagnosis for revoked Twitch EventSub subscriptions.</summary>
public sealed record EventSubRevocationGuidance(
    string Status,
    string Message,
    bool RequiresReauthorization,
    bool CanAttemptAutomaticRecovery = false);

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
                "La cuenta indicada por la condición de la suscripción ya no existe; no se puede restaurar esta suscripción con ese usuario.",
                false),
            "version_removed" => new(
                normalized,
                "Twitch retiró esta versión del tipo de evento; hace falta actualizar la definición en VTuberBot antes de registrarla de nuevo.",
                false),
            "moderator_removed" => new(
                normalized,
                "La cuenta que había concedido el permiso de moderador ya no es moderadora del canal; restaurá ese rol o volvé a autorizar con una cuenta con permisos.",
                false),
            "websocket_network_timeout" or "websocket_network_error" or "websocket_internal_error" =>
                new(normalized,
                    "Twitch informó un fallo transitorio de red/servidor. VTuberBot puede intentar registrar de nuevo solo esta suscripción en la sesión WebSocket activa; si la sesión ya no sirve, la reconexión completa registrará las suscripciones de nuevo.",
                    false,
                    true),
            "websocket_disconnected" or "websocket_failed_ping_pong" or "websocket_received_inbound_traffic" or "websocket_connection_unused" =>
                new(normalized,
                    "La sesión WebSocket indica una desconexión o un problema del lado cliente. No se reintentará esta suscripción de forma aislada: comprobá el estado de la sesión; si la conexión se perdió, la reconexión completa la registrará de nuevo.",
                    false),
            "" => new(
                "unknown",
                "Twitch no proporcionó el motivo de revocación; comprobá el diagnóstico y los permisos antes de volver a crear la suscripción.",
                false),
            _ => new(
                normalized,
                "El motivo no se reconoce en esta versión. No se intentará restaurar silenciosamente; comprobá el estado de la suscripción y los permisos requeridos.",
                false)
        };
    }
}
