using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Errores;

/// <summary>
/// Una capa determinista (L2a, L2b, L3, L4 o L5) denego el acceso: 403-acceso (SPEC 3.4). Aborta toda la peticion.
/// Lleva el rol (para elegir el mensaje) y el motivo de auditoria; tokens y costo reflejan lo consumido antes del rechazo.
/// </summary>
public sealed class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(RolAcademico rol, string motivo, int tokens = 0, decimal costoUsd = 0m)
        : base(motivo)
    {
        Rol = rol;
        Motivo = motivo;
        Tokens = tokens;
        CostoUsd = costoUsd;
    }

    public RolAcademico Rol { get; }

    /// <summary>Motivo de auditoria: referencia, herramienta, politica o salida.</summary>
    public string Motivo { get; }

    public int Tokens { get; }

    public decimal CostoUsd { get; }
}
