using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;

namespace GestorIncidentesTI.Tests;

public class CumplimientoSlaTests
{
    private static Incidente Nuevo(EstadoIncidente estado, bool incumplido) =>
        new() { Titulo = "Prueba", Descripcion = "Prueba", Estado = estado, SlaIncumplido = incumplido };

    [Fact]
    public void Solo_cuentan_los_resueltos_o_cerrados()
    {
        var incidentes = new[]
        {
            Nuevo(EstadoIncidente.Cerrado, incumplido: false),
            Nuevo(EstadoIncidente.Resuelto, incumplido: true),
            Nuevo(EstadoIncidente.Abierto, incumplido: true), // abierto: no entra en el cumplimiento
        };

        Assert.Equal(50.0, SlaService.CalcularCumplimiento(incidentes));
    }

    [Fact]
    public void Sin_incidentes_cerrados_el_cumplimiento_es_total()
    {
        Assert.Equal(100.0, SlaService.CalcularCumplimiento(new[] { Nuevo(EstadoIncidente.Abierto, incumplido: true) }));
    }
}
