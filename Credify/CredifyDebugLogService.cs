using Microsoft.Extensions.Logging;

namespace Credify;

/// <summary>
/// Binds a DI-provided <see cref="ILogger"/> to the static <see cref="CredifyDebugLog"/> facade so the
/// hand-constructed game cores (which never receive a logger of their own) can still write to the host's
/// configured logger. Registered as a singleton and taken as a <c>Plugin</c> constructor dependency, so
/// the host's DI builds it — and runs this binding — when the plugin is created, before any game loop
/// starts. Remove together with <see cref="CredifyDebugLog"/> once the flow debugging is done.
/// </summary>
public sealed class CredifyDebugLogService
{
    public CredifyDebugLogService(ILogger<CredifyDebugLogService> logger)
    {
        CredifyDebugLog.Logger = logger;
        logger.LogInformation("[CredifyDbg] debug logging bound to host logger");
    }
}
