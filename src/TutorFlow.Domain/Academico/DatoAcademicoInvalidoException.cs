namespace TutorFlow.Domain.Academico;

/// <summary>
/// Dato academico corrupto (nota fuera de [0.0, 5.0], clases programadas &lt;= 0 o nulas, creditos &lt;= 0, etc.; SPEC 5.1, 5.3).
/// Nunca se recorta ni se inventa un valor: quien lo captura informa el error.
/// </summary>
public sealed class DatoAcademicoInvalidoException : Exception
{
    public DatoAcademicoInvalidoException(string message)
        : base(message)
    {
    }
}
