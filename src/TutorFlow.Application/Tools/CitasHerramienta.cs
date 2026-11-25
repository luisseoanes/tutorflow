using TutorFlow.Application.Ports;

namespace TutorFlow.Application.Tools;

/// <summary>Citas deterministas de herramientas (SPEC 7.5.2 y 7.5.3). Normativas primero, luego las de datos; sin duplicados.</summary>
internal sealed class CitasHerramienta
{
    private const string Evaluacion = "evaluacion_y_calificaciones.md";
    private const string Reglamento = "reglamento_academico_2025.md";
    private const string Bienestar = "bienestar_y_becas.md";

    private static readonly CitaDocumental[] Orden =
    [
        new(Evaluacion, "Sección 3"),
        new(Evaluacion, "Sección 4"),
        new(Reglamento, "Artículo 5"),
        new(Reglamento, "Artículo 6"),
        new(Reglamento, "Artículo 11"),
        new(Reglamento, "Artículo 12"),
        new(Reglamento, "Artículo 20"),
        new(Bienestar, "Sección 3.1"),
    ];

    private readonly HashSet<CitaDocumental> _normativas = [];
    private readonly List<CitaDocumental> _datos = [];

    public CitasHerramienta Definitiva() => Normativa(0);

    public CitasHerramienta Promedio() => Normativa(1);

    public CitasHerramienta AprobadaOPerdida() => Normativa(2);

    public CitasHerramienta Habilitacion() => Normativa(3);

    public CitasHerramienta PerdidaPorInasistencia() => Normativa(4);

    public CitasHerramienta AsistenciaNoRegistrada() => Normativa(5);

    public CitasHerramienta SituacionAcademica() => Normativa(6);

    public CitasHerramienta Beca() => Normativa(7);

    public CitasHerramienta DatosEstudiante(string seccion) => Dato(new CitaDocumental("Datos del estudiante", seccion));

    public CitasHerramienta DatosPrograma(string programaId) => Dato(new CitaDocumental("Datos del programa", programaId));

    public IReadOnlyList<CitaDocumental> Construir() =>
        Orden.Where(_normativas.Contains).Concat(_datos).ToList();

    private CitasHerramienta Normativa(int indice)
    {
        _normativas.Add(Orden[indice]);
        return this;
    }

    private CitasHerramienta Dato(CitaDocumental cita)
    {
        if (!_datos.Contains(cita))
        {
            _datos.Add(cita);
        }

        return this;
    }
}
