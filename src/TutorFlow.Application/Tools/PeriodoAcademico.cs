using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Academico;

namespace TutorFlow.Application.Tools;

internal sealed record AsignaturaConNombre(AsignaturaPeriodo Dominio, string Nombre);

/// <summary>
/// Une notas y asistencia de la fuente en objetos de dominio (que validan y calculan) y los serializa a JSON invariante.
/// Aqui no se calcula ninguna regla academica: todo viene de <c>TutorFlow.Domain</c>.
/// </summary>
internal static class PeriodoAcademico
{
    public static List<AsignaturaConNombre> Construir(IReadOnlyList<NotaFuente> notas, IReadOnlyList<AsistenciaFuente> asistencia)
    {
        var resultado = new List<AsignaturaConNombre>(notas.Count);
        foreach (var n in notas)
        {
            var a = asistencia.FirstOrDefault(x => string.Equals(x.Codigo, n.Codigo, StringComparison.Ordinal));
            try
            {
                // Sin fila de asistencia = clases nulas = dato corrupto (SPEC 5.3): el dominio lo rechaza.
                var dominio = new AsignaturaPeriodo(n.Codigo, n.Creditos, n.Corte1, n.Corte2, n.Corte3, a?.ClasesProgramadas, a?.Inasistencias);
                resultado.Add(new AsignaturaConNombre(dominio, n.Asignatura));
            }
            catch (ArgumentException ex)
            {
                throw new DatoAcademicoInvalidoException($"Dato de asignatura invalido: {ex.ParamName}.");
            }
        }

        return resultado;
    }

    public static string Estado(AsignaturaPeriodo a)
    {
        if (a.PierdePorInasistencia == true)
        {
            return "perdida-inasistencia";
        }

        if (a.Perdida)
        {
            return "perdida";
        }

        return a.Aprobada ? "aprobada" : "en-curso";
    }

    public static string Json(Action<Utf8JsonWriter> escribir)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            escribir(w);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>decimal nulo -> null JSON; decimal -> numero con punto (Utf8JsonWriter usa formato invariante).</summary>
    public static void Num(Utf8JsonWriter w, string nombre, decimal? valor)
    {
        if (valor is null)
        {
            w.WriteNull(nombre);
        }
        else
        {
            w.WriteNumber(nombre, valor.Value);
        }
    }

    public static void Bool(Utf8JsonWriter w, string nombre, bool? valor)
    {
        if (valor is null)
        {
            w.WriteNull(nombre);
        }
        else
        {
            w.WriteBoolean(nombre, valor.Value);
        }
    }

    /// <summary>Citas normativas que implican los datos del periodo: pérdida por inasistencia (Art. 11) y asistencia no registrada (Art. 12).</summary>
    public static void CitarAsistencia(CitasHerramienta citas, IEnumerable<AsignaturaConNombre> periodo)
    {
        foreach (var a in periodo)
        {
            if (a.Dominio.PierdePorInasistencia == true)
            {
                citas.PerdidaPorInasistencia();
            }
            else if (a.Dominio.PierdePorInasistencia is null)
            {
                citas.AsistenciaNoRegistrada();
            }
        }
    }
}
