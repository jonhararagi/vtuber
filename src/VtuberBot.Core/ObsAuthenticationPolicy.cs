namespace VtuberBot.Core;

/// <summary>Requires an OBS WebSocket handshake protected by the server's password challenge.</summary>
public static class ObsAuthenticationPolicy
{
    public static void ValidateHandshake(bool serverRequiresAuthentication, string? password)
    {
        if (!serverRequiresAuthentication)
            throw new InvalidOperationException(
                "OBS WebSocket no exige autenticación. Activá la contraseña en Herramientas → Ajustes del servidor WebSocket para conectar de forma segura.");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "OBS requiere una contraseña de WebSocket. Ingresala en la configuración de VTuberBot.");
    }
}
