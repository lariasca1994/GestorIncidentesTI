using GestorIncidentesTI.Data;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Registro de eventos de seguridad de las cuentas: qué pasó, a qué cuenta y cuándo; nunca
/// contraseñas, códigos ni datos del equipo. Cada registro borra los de más de 15 días.
/// Usa SQL directo (sin el rastreo de EF) para no mezclarse con otros cambios pendientes, y si
/// la base no responde el evento se pierde pero el ingreso sigue.
/// </summary>
public class Auditoria
{
    public const string Ingreso = "ingreso";
    public const string ClaveErrada = "clave_errada";
    public const string CodigoErrado = "codigo_errado";
    public const string CuentaBloqueada = "cuenta_bloqueada";
    public const string ClaveCambiada = "clave_cambiada";
    public const string Recuperacion = "recuperacion_solicitada";
    public const string MetodoActivado = "metodo_activado";

    private const int RetencionDias = 15;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<Auditoria> _logger;

    public Auditoria(ApplicationDbContext db, ILogger<Auditoria> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RegistrarAsync(string tipo, ApplicationUser usuario)
    {
        try
        {
            var ahora = DateTime.UtcNow;
            var limite = ahora.AddDays(-RetencionDias);
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM EventosSeguridad WHERE Fecha < {limite}");
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO EventosSeguridad (Tipo, UsuarioId, Fecha) VALUES ({tipo}, {usuario.Id}, {ahora})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar el evento de seguridad {Tipo}", tipo);
        }
    }
}
