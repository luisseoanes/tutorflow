using System.Text.Json;
using TutorFlow.Api.Contracts;

namespace TutorFlow.Api.Tests.Dtos;

public class ContractSerializationTests
{
    [Fact]
    [Trait("Contrato", "SPEC-3.1")]
    public void PreguntarResponse_serializa_con_los_nombres_exactos_del_contrato()
    {
        var dto = new PreguntarResponse(
            "hola",
            [new CitaDto("reglamento.md", "Art. 5")],
            true,
            12,
            0.001234m);

        var json = JsonSerializer.Serialize(dto);

        Assert.Equal(
            "{\"respuesta\":\"hola\",\"citas\":[{\"documento\":\"reglamento.md\",\"seccion\":\"Art. 5\"}],"
            + "\"usoDatosEstudiante\":true,\"tokens\":12,\"costoUsd\":0.001234}",
            json);
    }

    [Fact]
    [Trait("Contrato", "SPEC-3.1")]
    public void PreguntarResponse_citas_vacias_se_serializan_como_arreglo()
    {
        var json = JsonSerializer.Serialize(new PreguntarResponse("x", [], false, 0, 0m));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(
            ["respuesta", "citas", "usoDatosEstudiante", "tokens", "costoUsd"],
            root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("citas").ValueKind);
        Assert.Equal(0, root.GetProperty("citas").GetArrayLength());
    }

    [Fact]
    [Trait("Contrato", "SPEC-3.1")]
    public void SaludResponse_serializa_exactamente_estado_ok()
    {
        Assert.Equal("{\"estado\":\"ok\"}", JsonSerializer.Serialize(new SaludResponse("ok")));
    }
}
