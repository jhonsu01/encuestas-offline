using Encuestas.Core;

namespace EncuestasCentral.Api;

/// <summary>Estado en memoria compartido entre la UI (WPF) y la API (Kestrel).</summary>
public class ServerState
{
    public List<Survey> Surveys { get; } = new();
    public event Action<string>? OnLog;

    public void Log(string message) => OnLog?.Invoke(message);

    public void PublishSurvey(Survey survey)
    {
        Surveys.RemoveAll(s => s.Id == survey.Id);
        Surveys.Add(survey);
    }
}
