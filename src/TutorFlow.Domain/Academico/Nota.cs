namespace TutorFlow.Domain.Academico;

/// <summary>Validacion de notas: rango [0.0, 5.0] (SPEC 5.1). Fuera de rango = dato corrupto; no se recorta.</summary>
public static class Nota
{
    public const decimal Minima = 0.0m;
    public const decimal Maxima = 5.0m;

    public static bool EsValida(decimal valor) => valor >= Minima && valor <= Maxima;

    public static decimal Validar(decimal valor, string campo)
    {
        if (!EsValida(valor))
        {
            throw new DatoAcademicoInvalidoException($"Nota fuera de rango [0.0, 5.0] en '{campo}'.");
        }

        return valor;
    }
}
