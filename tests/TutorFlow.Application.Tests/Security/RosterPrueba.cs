using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tests.Security;

/// <summary>Roster y principales de prueba con los datos del material (SOLO en tests, RES-12).</summary>
internal static class RosterPrueba
{
    public static RosterSnapshot Material() => new(
        [
            new("EST-001", "laura gomez", "ING-SIS"),
            new("EST-002", "andres martinez", "ING-SIS"),
            new("EST-003", "sofia herrera", "ING-SIS"),
            new("EST-004", "camilo rojas", "ADM"),
            new("EST-005", "valentina castro", "ADM"),
            new("EST-006", "mateo vargas", "ADM"),
        ],
        [new("COORD-01"), new("COORD-02")],
        [
            new("ING-SIS", "ingenieria de sistemas"),
            new("ADM", "administracion de empresas"),
        ],
        DateTimeOffset.UnixEpoch);

    public static PrincipalAcademico Estudiante(string id) => new(RolAcademico.Estudiante, id);

    public static PrincipalAcademico Coordinador(string id, params string[] programas) =>
        new(RolAcademico.Coordinador, id, programas);

    public static PrincipalAcademico Est001 => Estudiante("EST-001");

    public static PrincipalAcademico Est004 => Estudiante("EST-004");

    public static PrincipalAcademico Coord01 => Coordinador("COORD-01", "ING-SIS");

    public static PrincipalAcademico Coord02 => Coordinador("COORD-02", "ADM");
}
