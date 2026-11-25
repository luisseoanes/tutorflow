using System.Globalization;
using TutorFlow.Domain.Academico;

namespace TutorFlow.Domain.Tests;

public class PorcentajeInasistenciaTests
{
    [Theory]
    [InlineData(48, 9, "18.75")]
    [InlineData(64, 6, "9.38")]
    [InlineData(48, 0, "0.00")]
    public void Porcentaje_con_dos_decimales_away_from_zero(int clases, int faltas, string esperado) =>
        Assert.Equal(decimal.Parse(esperado, CultureInfo.InvariantCulture), Inasistencia.Porcentaje(clases, faltas));

    [Fact]
    public void Sin_registro_es_nulo_y_el_dato_corrupto_lanza()
    {
        Assert.Null(Inasistencia.Porcentaje(32, null));
        Assert.Throws<DatoAcademicoInvalidoException>(() => Inasistencia.Porcentaje(0, 1));
        Assert.Throws<DatoAcademicoInvalidoException>(() => Inasistencia.Porcentaje(null, 1));
        Assert.Throws<DatoAcademicoInvalidoException>(() => Inasistencia.Porcentaje(48, -1));
    }
}
