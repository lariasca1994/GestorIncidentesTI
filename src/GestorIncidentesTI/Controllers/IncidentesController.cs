using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISlaService _slaService;

    public IncidentesController(ApplicationDbContext db, ISlaService slaService)
    {
        _db = db;
        _slaService = slaService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<IncidenteResumenDto>>> Listar(
        [FromQuery] EstadoIncidente? estado)
    {
        var query = _db.Incidentes.AsQueryable();
        if (estado is not null) query = query.Where(i => i.Estado == estado);

        var resultado = await query
            .OrderByDescending(i => i.FechaCreacion)
            .Select(i => new IncidenteResumenDto(
                i.Id, i.Titulo, i.Prioridad, i.Estado, i.NivelActual,
                i.FechaCreacion, i.FechaLimiteSla, i.SlaIncumplido))
            .ToListAsync();

        return Ok(resultado);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Incidente>> Obtener(int id)
    {
        var incidente = await _db.Incidentes
            .Include(i => i.Historial)
            .FirstOrDefaultAsync(i => i.Id == id);

        return incidente is null ? NotFound() : Ok(incidente);
    }

    [HttpPost]
    public async Task<ActionResult<Incidente>> Crear(CrearIncidenteDto dto)
    {
        var ahora = DateTime.UtcNow;
        var fechaLimite = await _slaService.CalcularFechaLimiteAsync(dto.Prioridad, ahora);

        var incidente = new Incidente
        {
            Titulo = dto.Titulo,
            Descripcion = dto.Descripcion,
            Categoria = dto.Categoria,
            Prioridad = dto.Prioridad,
            SolicitanteNombre = dto.SolicitanteNombre,
            FechaCreacion = ahora,
            FechaLimiteSla = fechaLimite,
            Estado = EstadoIncidente.Abierto,
            NivelActual = NivelEscalamiento.N1
        };

        incidente.Historial.Add(new HistorialEstado
        {
            EstadoAnterior = EstadoIncidente.Abierto,
            EstadoNuevo = EstadoIncidente.Abierto,
            NivelAnterior = NivelEscalamiento.N1,
            NivelNuevo = NivelEscalamiento.N1,
            Comentario = "Incidente creado."
        });

        _db.Incidentes.Add(incidente);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = incidente.Id }, incidente);
    }

    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoDto dto)
    {
        var incidente = await _db.Incidentes.FindAsync(id);
        if (incidente is null) return NotFound();

        var estadoAnterior = incidente.Estado;
        incidente.Estado = dto.NuevoEstado;

        if (dto.NuevoEstado is EstadoIncidente.Resuelto or EstadoIncidente.Cerrado)
        {
            incidente.FechaResolucion = DateTime.UtcNow;
            if (incidente.FechaLimiteSla is not null)
                incidente.SlaIncumplido = incidente.FechaResolucion > incidente.FechaLimiteSla;
        }

        _db.HistorialEstados.Add(new HistorialEstado
        {
            IncidenteId = incidente.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = incidente.Estado,
            NivelAnterior = incidente.NivelActual,
            NivelNuevo = incidente.NivelActual,
            Comentario = dto.Comentario
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult> Dashboard()
    {
        var abiertos = await _db.Incidentes.CountAsync(i =>
            i.Estado != EstadoIncidente.Resuelto && i.Estado != EstadoIncidente.Cerrado);

        var incumplidos = await _db.Incidentes.CountAsync(i => i.SlaIncumplido);
        var cumplimiento = await _slaService.CalcularCumplimientoAsync();

        return Ok(new
        {
            IncidentesAbiertos = abiertos,
            IncidentesConSlaIncumplido = incumplidos,
            PorcentajeCumplimientoSla = cumplimiento
        });
    }
}
