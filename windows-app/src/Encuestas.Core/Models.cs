namespace Encuestas.Core;

/// <summary>Formulario de encuesta (contrato del shared-schema).</summary>
public class Survey
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Schedule? Schedule { get; set; }
    public List<Question> Questions { get; set; } = new();
}

public class Schedule
{
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Timezone { get; set; }
}

public class Question
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "text";
    public string Label { get; set; } = "";
    public bool Required { get; set; }
    public List<string>? Options { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
}
