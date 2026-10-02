using System.Net;
using System.Text;

namespace GestorIncidentesTI.Services;

/// <summary>Una etiqueta de color (estado, prioridad, SLA), como los .badge del sitio.</summary>
public sealed record Chip(string Texto, string Color, string Fondo);

/// <summary>Contenido de un aviso por correo, independiente del HTML.</summary>
public sealed record Aviso(
    string Encabezado,
    string Titulo,
    IReadOnlyList<Chip> Chips,
    IReadOnlyList<(string Campo, string Valor)> Detalles,
    IReadOnlyList<(string Etiqueta, string Texto)> Notas,
    string? Enlace,
    string TextoBoton);

/// <summary>
/// HTML del correo con la paleta del tema claro de site.css. Estilos en línea
/// y maquetación con tablas: es lo único que respetan todos los clientes de correo.
/// Todo texto que viene del usuario se codifica (sin HTML inyectado).
/// </summary>
public static class PlantillaCorreo
{
    private const string Primario = "#0d9488";
    private const string PrimarioOscuro = "#0f766e";
    private const string Fondo = "#f4faf9";
    private const string Superficie = "#ffffff";
    private const string Superficie2 = "#e6f4f1";
    private const string Borde = "#cfe8e3";
    private const string ColorTexto = "#0f1e1c";
    private const string TextoSuave = "#45635d";
    private const string Fuente = "system-ui,-apple-system,'Segoe UI',Roboto,Arial,sans-serif";

    public static string Html(Aviso aviso)
    {
        var html = new StringBuilder();
        html.Append($"""
            <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(aviso.Titulo)}</title></head>
            <body style="margin:0;padding:0;background:{Fondo};">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Fondo};padding:24px 12px;">
            <tr><td align="center">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:{Superficie};border:1px solid {Borde};border-radius:10px;overflow:hidden;font-family:{Fuente};color:{ColorTexto};">
            <tr><td style="background:{Primario};padding:18px 28px;">
              <div style="font-size:13px;font-weight:600;letter-spacing:.04em;text-transform:uppercase;color:#ccfbf1;">Gestor de Incidentes TI</div>
              <div style="font-size:15px;color:#ffffff;margin-top:2px;">{E(aviso.Encabezado)}</div>
            </td></tr>
            <tr><td style="padding:24px 28px 8px;">
              <h1 style="margin:0 0 12px;font-size:20px;line-height:1.35;color:{ColorTexto};">{E(aviso.Titulo)}</h1>
            """);

        if (aviso.Chips.Count > 0)
        {
            html.Append("<div style=\"margin:0 0 4px;\">");
            foreach (var chip in aviso.Chips)
                html.Append($"<span style=\"display:inline-block;margin:0 6px 6px 0;padding:3px 10px;border-radius:999px;font-size:12px;font-weight:600;color:{chip.Color};background:{chip.Fondo};\">{E(chip.Texto)}</span>");
            html.Append("</div>");
        }

        html.Append($"""
            </td></tr>
            <tr><td style="padding:8px 28px 4px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border:1px solid {Borde};border-radius:10px;border-collapse:separate;">
            """);

        for (var i = 0; i < aviso.Detalles.Count; i++)
        {
            var (campo, valor) = aviso.Detalles[i];
            var separador = i == 0 ? "" : $"border-top:1px solid {Borde};";
            html.Append($"""
                <tr>
                  <td style="{separador}padding:10px 14px;width:38%;font-size:13px;color:{TextoSuave};vertical-align:top;">{E(campo)}</td>
                  <td style="{separador}padding:10px 14px;font-size:14px;font-weight:600;color:{ColorTexto};vertical-align:top;">{E(valor)}</td>
                </tr>
                """);
        }

        html.Append("</table></td></tr>");

        if (aviso.Notas.Count > 0)
        {
            html.Append($"""
                <tr><td style="padding:20px 28px 4px;">
                  <div style="font-size:13px;font-weight:600;letter-spacing:.04em;text-transform:uppercase;color:{PrimarioOscuro};margin-bottom:8px;">Notas</div>
                """);
            foreach (var (etiqueta, texto) in aviso.Notas)
            {
                html.Append($"""
                    <div style="background:{Superficie2};border-left:3px solid {Primario};border-radius:6px;padding:12px 14px;margin-bottom:10px;">
                      <div style="font-size:12px;color:{TextoSuave};margin-bottom:4px;">{E(etiqueta)}</div>
                      <div style="font-size:14px;line-height:1.55;color:{ColorTexto};">{ConSaltos(texto)}</div>
                    </div>
                    """);
            }
            html.Append("</td></tr>");
        }

        if (aviso.Enlace is not null)
        {
            html.Append($"""
                <tr><td style="padding:18px 28px 8px;">
                  <a href="{E(aviso.Enlace)}" style="display:inline-block;background:{Primario};color:#ffffff;text-decoration:none;font-weight:600;font-size:14px;padding:10px 22px;border-radius:10px;">{E(aviso.TextoBoton)}</a>
                </td></tr>
                """);
        }

        html.Append($"""
            <tr><td style="padding:20px 28px 22px;border-top:1px solid {Borde};font-size:12px;line-height:1.5;color:{TextoSuave};">
              Aviso automático del Gestor de Incidentes TI. Lo reciben quien hizo la gestión y los administradores.
            </td></tr>
            </table>
            </td></tr></table>
            </body></html>
            """);

        return html.ToString();
    }

    /// <summary>Versión en texto plano (mejora la entrega y la lectura en clientes simples).</summary>
    public static string Texto(Aviso aviso)
    {
        var texto = new StringBuilder();
        texto.AppendLine($"Gestor de Incidentes TI — {aviso.Encabezado}");
        texto.AppendLine();
        texto.AppendLine(aviso.Titulo);
        if (aviso.Chips.Count > 0) texto.AppendLine(string.Join(" · ", aviso.Chips.Select(c => c.Texto)));
        texto.AppendLine();
        foreach (var (campo, valor) in aviso.Detalles) texto.AppendLine($"{campo}: {valor}");
        if (aviso.Notas.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("NOTAS");
            foreach (var (etiqueta, nota) in aviso.Notas)
            {
                texto.AppendLine($"{etiqueta}:");
                texto.AppendLine(nota);
                texto.AppendLine();
            }
        }
        if (aviso.Enlace is not null) texto.AppendLine($"{aviso.TextoBoton}: {aviso.Enlace}");
        return texto.ToString();
    }

    /// <summary>
    /// Mismo contenido para Telegram: solo admite negrita, cursiva y enlaces,
    /// y hasta 4.096 caracteres, así que va resumido y sin estilos.
    /// </summary>
    public static string Telegram(Aviso aviso)
    {
        var texto = new StringBuilder();
        texto.AppendLine($"🛠️ <b>{E(aviso.Encabezado)}</b> · Gestor de Incidentes TI");
        texto.AppendLine($"<b>{E(aviso.Titulo)}</b>");
        if (aviso.Chips.Count > 0) texto.AppendLine(E(string.Join(" · ", aviso.Chips.Select(c => c.Texto))));
        texto.AppendLine();
        foreach (var (campo, valor) in aviso.Detalles) texto.AppendLine($"<b>{E(campo)}:</b> {E(valor)}");
        foreach (var (etiqueta, nota) in aviso.Notas)
        {
            texto.AppendLine();
            texto.AppendLine($"📝 <i>{E(etiqueta)}</i>");
            texto.AppendLine(E(nota.Length > 1200 ? nota[..1200] + "…" : nota));
        }
        var resultado = texto.ToString().TrimEnd();
        return resultado.Length > 4000 ? resultado[..4000] : resultado;
    }

    private static string E(string texto) => WebUtility.HtmlEncode(texto);

    private static string ConSaltos(string texto) =>
        E(texto.Replace("\r\n", "\n").Trim()).Replace("\n", "<br>");
}
