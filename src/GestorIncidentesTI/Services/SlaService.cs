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

    public async Task<DateTime> CalcularFechaLimiteAsync(PrioridadIncidente prioridad, DateTime fechaCreacion)
    {
        var sla = await _db.SlaDefiniciones
            .FirstOrDefaultAsync(s => s.Prioridad == prioridad)
            ?? throw new InvalidOperationException($"No hay SLA definido para la prioridad {prioridad}.");

        return fechaCreacion.AddHours(sla.HorasResolucion);
    }

    public async Task<int> EvaluarEscalamientosAsync()
    {
        var incidentesActivos = await _db.Incidentes
            .Where(i => i.Estado == EstadoIncidente.Abierto || i.Estado == EstadoIncidente.EnProgreso)
            .ToListAsync();

        var slaPorPrioridad = await _db.SlaDefiniciones.ToDictionaryAsync(s => s.Prioridad);

        int escalados = 0;
        var ahora = DateTime.UtcNow;

        foreach (var incidente in incidentesActivos)
        {
            if (incidente.FechaLimiteSla is null) continue;
            if (!slaPorPrioridad.TryGetValue(incidente.Prioridad, out var sla)) continue;

            var tiempoTotal = (incidente.FechaLimiteSla.Value - incidente.FechaCreacion).TotalHours;
            var tiempoTranscurrido = (ahora - incidente.FechaCreacion).TotalHours;
            var proporcionTranscurrida = tiempoTranscurrido / tiempoTotal;

            var siguienteNivel = incidente.NivelActual switch
            {
                NivelEscalamiento.N1 => NivelEscalamiento.N2,
                NivelEscalamiento.N2 => NivelEscalamiento.N3,
                _ => (NivelEscalamiento?)null
            };

            if (siguienteNivel is null) continue;

            bool debeEscalar = proporcionTranscurrida >= sla.UmbralEscalamiento;
            bool yaVencio = ahora > incidente.FechaLimiteSla;

            if (debeEscalar || yaVencio)
            {
                var estadoAnterior = incidente.Estado;
                var nivelAnterior = incidente.NivelActual;

                incidente.NivelActual = siguienteNivel.Value;
                incidente.Estado = EstadoIncidente.Escalado;
                if (yaVencio) incidente.SlaIncumplido = true;

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

        if (escalados > 0) await _db.SaveChangesAsync();
        return escalados;
    }

    public async Task<double> CalcularCumplimientoAsync()
    {
        var resueltos = await _db.Incidentes
            .Where(i => i.Estado == EstadoIncidente.Resuelto || i.Estado == EstadoIncidente.Cerrado)
            .ToListAsync();

        if (resueltos.Count == 0) return 100.0;

        var cumplidos = resueltos.Count(i => !i.SlaIncumplido);
        return Math.Round((double)cumplidos / resueltos.Count * 100, 1);
    }
}
