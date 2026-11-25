using TutorFlow.Application.Ports;

namespace TutorFlow.Application.Audit;

/// <inheritdoc />
public sealed class ContextoAuditoria : IContextoAuditoria
{
    private const int MaxPregunta = 1000;
    private const int MaxCampo = 64;

    private readonly object _lock = new();
    private readonly List<string> _estudiantes = [];
    private readonly List<string> _programas = [];
    private readonly List<string> _denegados = [];
    private readonly List<HerramientaAuditada> _herramientas = [];
    private readonly List<string> _citas = [];
    private string _pregunta = string.Empty;
    private UsuarioAuditado? _usuario;
    private bool _identidadValida;
    private string? _motivo;
    private string? _estrategia;
    private string? _modelo;
    private int _tokens;
    private decimal _costo;
    private bool _emitido;

    public void Iniciar(string pregunta, string? rol, string? id)
    {
        lock (_lock)
        {
            _pregunta = pregunta ?? string.Empty;
            _usuario = rol is not null && id is not null ? new UsuarioAuditado(Acotar(rol), Acotar(id)) : null;
        }
    }

    public void IdentidadValidada()
    {
        lock (_lock)
        {
            _identidadValida = true;
        }
    }

    public void ObjetivosDenegados(IEnumerable<string> objetivos)
    {
        ArgumentNullException.ThrowIfNull(objetivos);
        lock (_lock)
        {
            foreach (var o in objetivos)
            {
                Agregar(_denegados, Acotar(o));
            }
        }
    }

    public void Denegar(string motivo)
    {
        lock (_lock)
        {
            _motivo ??= motivo;
        }
    }

    public void HerramientaDenegada(string nombre, string? objetivo)
    {
        lock (_lock)
        {
            _herramientas.Add(new HerramientaAuditada(Acotar(nombre), objetivo is null ? null : Acotar(objetivo), "denegado"));
        }
    }

    public void ConsultaExitosa(string nombre, string objetivo, bool esPrograma)
    {
        lock (_lock)
        {
            _herramientas.Add(new HerramientaAuditada(Acotar(nombre), objetivo, "permitido"));
            if (esPrograma)
            {
                foreach (var p in objetivo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    Agregar(_programas, p);
                }
            }
            else
            {
                Agregar(_estudiantes, objetivo);
            }
        }
    }

    public void FlujoLlm(IEnumerable<string>? citas = null, string? estrategia = null, string? modelo = null, int? tokens = null, decimal? costoUsd = null)
    {
        lock (_lock)
        {
            if (citas is not null)
            {
                foreach (var c in citas)
                {
                    Agregar(_citas, c);
                }
            }

            _estrategia = estrategia ?? _estrategia;
            _modelo = modelo ?? _modelo;
            _tokens = tokens ?? _tokens;
            _costo = costoUsd ?? _costo;
        }
    }

    public EventoPregunta? ConstruirUnaVez(string requestId, DateTimeOffset ts, int resultado, long latenciaMs)
    {
        lock (_lock)
        {
            if (_emitido)
            {
                return null;
            }

            _emitido = true;
            var pregunta = _pregunta.Length > MaxPregunta ? _pregunta[..MaxPregunta] : _pregunta;
            return new EventoPregunta(
                ts,
                requestId,
                _usuario,
                _identidadValida,
                pregunta,
                _estudiantes.ToArray(),
                _programas.ToArray(),
                _herramientas.ToArray(),
                resultado,
                _motivo,
                _citas.ToArray(),
                _estrategia,
                _modelo,
                _tokens,
                _costo,
                latenciaMs,
                _denegados.ToArray());
        }
    }

    private static void Agregar(List<string> lista, string valor)
    {
        if (!lista.Contains(valor, StringComparer.Ordinal))
        {
            lista.Add(valor);
        }
    }

    /// <summary>Acota campos que vienen del cliente (rol e id declarados) para que no inflen el log.</summary>
    private static string Acotar(string valor) => valor.Length > MaxCampo ? valor[..MaxCampo] : valor;
}
