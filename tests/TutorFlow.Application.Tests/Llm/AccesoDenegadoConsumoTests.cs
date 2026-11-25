using TutorFlow.Application.Errores;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;
using static TutorFlow.Application.Tests.Llm.Dobles;

namespace TutorFlow.Application.Tests.Llm;

public class AccesoDenegadoConsumoTests
{
    private static Task Generar(ILlmClient llm, DespachadorGuionizado d) =>
        new GeneradorRespuesta(llm, new GeneradorOptions(PrecioEntradaPorMtok: 1m, PrecioSalidaPorMtok: 5m))
            .GenerarAsync(Estudiante(), "¿Mis notas?", [], d, CancellationToken.None);

    [Fact]
    public async Task El_403_de_una_herramienta_lleva_los_tokens_y_el_costo_ya_consumidos()
    {
        var llm = new LlmGuionizado(Invoca("obtener_notas", entrada: 1000, salida: 200));
        var d = new DespachadorGuionizado(_ => throw new AccesoDenegadoException(RolAcademico.Estudiante, "L4:obtener_notas"));

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(() => Generar(llm, d));

        Assert.Equal(RolAcademico.Estudiante, ex.Rol);
        Assert.Equal("L4:obtener_notas", ex.Motivo);
        Assert.Equal(1200, ex.Tokens);
        Assert.Equal(0.002m, ex.CostoUsd); // (1000*1 + 200*5) / 1e6
    }

    [Fact]
    public async Task Otras_excepciones_se_propagan_intactas()
    {
        var llm = new LlmGuionizado(Invoca("obtener_notas"));
        var original = new InvalidOperationException("boom");
        var d = new DespachadorGuionizado(_ => throw original);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Generar(llm, d));

        Assert.Same(original, ex);
    }
}
