using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Services;

public class SlaService : ISlaService
{
    private readonly ApplicationDbContext _db;

    public SlaService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DateTime> CalcularFechaLimiteAsync(PrioridadIncidente prioridad, DateTime fechaCreacion) =>
        (await CalcularLimitesAsync(prioridad, fechaCreacion)).Resolucion;

    public async Task<(DateTime Respuesta, DateTime Resolucion)> CalcularLimitesAsync(PrioridadIncidente prioridad, DateTime fechaCreacion)
    {
        var sla = await _db.SlaDefiniciones
            .FirstOrDefaultAsync(s => s.Prioridad == prioridad)
            ?? throw new InvalidOperationException($"No hay SLA definido para la prioridad {prioridad}.");

        return (CalendarioSla.SumarHoras(fechaCreacion, sla.HorasRespuesta, prioridad),
                CalendarioSla.SumarHoras(fechaCreacion, sla.HorasResolucion, prioridad));
    }

    public async Task<int> EvaluarEscalamientosAsync()
    {
        var incidentesActivos = await _db.Incidentes
            .Where(i => i.Estado == EstadoIncidente.Abierto || i.Estado == EstadoIncidente.EnProgreso || i.Estado == EstadoIncidente.Escalado)
            .ToListAsync();

        var slaPorPrioridad = await _db.SlaDefiniciones.ToDictionaryAsync(s => s.Prioridad);

        int escalados = 0;
        bool hayCambios = false;
        var ahora = DateTime.UtcNow;

        foreach (var incidente in incidentesActivos)
        {
            if (incidente.FechaPrimeraRespuesta is null && !incidente.RespuestaIncumplida
                && incidente.FechaLimiteRespuesta is { } limiteRespuesta && ahora > limiteRespuesta)
            {
                incidente.RespuestaIncumplida = true;
                hayCambios = true;
            }

            if (incidente.FechaLimiteSla is null) continue;
            if (!slaPorPrioridad.TryGetValue(incidente.Prioridad, out var sla)) continue;

            // En horas de SLA (hábiles salvo Crítica) y sin contar la espera.
            var proporcionTranscurrida = CicloIncidente.FraccionSlaConsumida(incidente, ahora);

            var siguienteNivel = incidente.NivelActual switch
            {
                NivelEscalamiento.N1 => NivelEscalamiento.N2,
                NivelEscalamiento.N2 => NivelEscalamiento.N3,
                _ => (NivelEscalamiento?)null
            };

            bool yaVencio = ahora > incidente.FechaLimiteSla;

            if (yaVencio && !incidente.SlaIncumplido)
            {
                incidente.SlaIncumplido = true;
                hayCambios = true;
            }
            if (siguienteNivel is null) continue;

            // N1 escala al umbral del SLA; N2 lo hace a mitad del tiempo
            // restante, evitando dos escalaciones consecutivas inmediatas.
            var umbral = incidente.NivelActual == NivelEscalamiento.N1
                ? sla.UmbralEscalamiento
                : sla.UmbralEscalamiento + ((1 - sla.UmbralEscalamiento) / 2);
            bool debeEscalar = proporcionTranscurrida >= umbral;

            if (debeEscalar || yaVencio)
            {
                var estadoAnterior = incidente.Estado;
                var nivelAnterior = incidente.NivelActual;

                incidente.NivelActual = siguienteNivel.Value;
                incidente.Estado = EstadoIncidente.Escalado;
                _db.HistorialEstados.Add(new HistorialEstado
                {
                    IncidenteId = incidente.Id,
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = incidente.Estado,
                    NivelAnterior = nivelAnterior,
                    NivelNuevo = incidente.NivelActual,
                    Comentario = yaVencio
                        ? "Escalado automáticamente: SLA vencido."
                        : $"Escalado automáticamente: {proporcionTranscurrida:P0} del tiempo de SLA consumido."
                });

                escalados++;
            }
        }

        if (escalados > 0 || hayCambios) await _db.SaveChangesAsync();
        return escalados;
    }

    public async Task<double> CalcularCumplimientoAsync()
    {
        var resueltos = await _db.Incidentes
            .Where(i => i.Estado == EstadoIncidente.Resuelto || i.Estado == EstadoIncidente.Cerrado)
            .ToListAsync();

        return CalcularCumplimiento(resueltos);
    }

    public async Task<double> CalcularCumplimientoAsync(IQueryable<Incidente> incidentes)
    {
        var resueltos = await incidentes
            .Where(i => i.Estado == EstadoIncidente.Resuelto || i.Estado == EstadoIncidente.Cerrado)
            .ToListAsync();

        return CalcularCumplimiento(resueltos);
    }

    private static double CalcularCumplimiento(IReadOnlyCollection<Incidente> resueltos)
    {
        if (resueltos.Count == 0) return 100.0;

        var cumplidos = resueltos.Count(i => !i.SlaIncumplido);
        return Math.Round((double)cumplidos / resueltos.Count * 100, 1);
    }
}
