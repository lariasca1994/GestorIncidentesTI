using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestorIncidentesTI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
        var query = await ObtenerConsultaConAlcanceAsync();
        if (query is null) return Forbid();
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
        var query = await ObtenerConsultaConAlcanceAsync();
        if (query is null) return Forbid();

        var incidente = await query
            .Include(i => i.Historial)
            .FirstOrDefaultAsync(i => i.Id == id);

        return incidente is null ? NotFound() : Ok(incidente);
    }

    [HttpPost]
    public async Task<ActionResult<Incidente>> Crear(CrearIncidenteDto dto)
    {
        var proyectoId = await ObtenerProyectoParaCreacionAsync(dto.ProyectoId);
        if (proyectoId is null) return Forbid();
        if (!await _db.Proyectos.AnyAsync(p => p.Id == proyectoId.Value))
            return ValidationProblem("El proyecto indicado no existe.");

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
            NivelActual = NivelEscalamiento.N1,
            ProyectoId = proyectoId.Value,
            CreadoPorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty
        };

        incidente.Historial.Add(new HistorialEstado
        {
            EstadoAnterior = EstadoIncidente.Abierto,
            EstadoNuevo = EstadoIncidente.Abierto,
            NivelAnterior = NivelEscalamiento.N1,
            NivelNuevo = NivelEscalamiento.N1,
            Comentario = "Incidente creado.",
            ModificadoPorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });

        _db.Incidentes.Add(incidente);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = incidente.Id }, incidente);
    }

    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoDto dto)
    {
        var query = await ObtenerConsultaConAlcanceAsync();
        if (query is null) return Forbid();

        var incidente = await query.FirstOrDefaultAsync(i => i.Id == id);
        if (incidente is null) return NotFound();
        if (!EsTransicionValida(incidente.Estado, dto.NuevoEstado))
            return ValidationProblem($"No se permite cambiar de {incidente.Estado} a {dto.NuevoEstado}.");

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
            Comentario = dto.Comentario,
            ModificadoPorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult> Dashboard()
    {
        var query = await ObtenerConsultaConAlcanceAsync();
        if (query is null) return Forbid();

        var abiertos = await query.CountAsync(i =>
            i.Estado != EstadoIncidente.Resuelto && i.Estado != EstadoIncidente.Cerrado);

        var incumplidos = await query.CountAsync(i => i.SlaIncumplido);
        var cumplimiento = await _slaService.CalcularCumplimientoAsync(query);

        return Ok(new
        {
            IncidentesAbiertos = abiertos,
            IncidentesConSlaIncumplido = incumplidos,
            PorcentajeCumplimientoSla = cumplimiento
        });
    }

    private async Task<IQueryable<Incidente>?> ObtenerConsultaConAlcanceAsync()
    {
        var query = _db.Incidentes.AsQueryable();
        if (User.IsInRole("Admin")) return query;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var proyectoId = await _db.Users.Where(u => u.Id == userId)
            .Select(u => u.ProyectoId).SingleOrDefaultAsync();
        return proyectoId is null ? null : query.Where(i => i.ProyectoId == proyectoId.Value);
    }

    private async Task<int?> ObtenerProyectoParaCreacionAsync(int? proyectoSolicitado)
    {
        if (User.IsInRole("Admin")) return proyectoSolicitado;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;

        return await _db.Users.Where(u => u.Id == userId)
            .Select(u => u.ProyectoId).SingleOrDefaultAsync();
    }

    private static bool EsTransicionValida(EstadoIncidente actual, EstadoIncidente nuevo) =>
        (actual, nuevo) switch
        {
            (EstadoIncidente.Abierto, EstadoIncidente.EnProgreso or EstadoIncidente.Escalado or EstadoIncidente.Resuelto) => true,
            (EstadoIncidente.EnProgreso, EstadoIncidente.Escalado or EstadoIncidente.Resuelto) => true,
            (EstadoIncidente.Escalado, EstadoIncidente.EnProgreso or EstadoIncidente.Resuelto) => true,
            (EstadoIncidente.Resuelto, EstadoIncidente.Cerrado or EstadoIncidente.EnProgreso) => true,
            _ => false
        };
}
