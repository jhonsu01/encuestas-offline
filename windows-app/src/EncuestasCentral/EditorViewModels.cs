using Encuestas.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EncuestasCentral;

/// <summary>Opción de tipo de pregunta (código interno + etiqueta amigable).</summary>
public class QuestionTypeOption
{
    public string Code { get; set; } = "";
    public string Display { get; set; } = "";
}

public static class QuestionTypes
{
    public static readonly List<QuestionTypeOption> All = new()
    {
        new() { Code = "text",            Display = "Texto" },
        new() { Code = "single_choice",   Display = "Selección única" },
        new() { Code = "multiple_choice", Display = "Selección múltiple" },
        new() { Code = "number",          Display = "Numérico" },
        new() { Code = "date",            Display = "Fecha" },
        new() { Code = "image",           Display = "Imagen (foto)" },
        new() { Code = "gps",             Display = "Ubicación GPS" },
    };
}

/// <summary>Una pregunta en el constructor visual.</summary>
public class QuestionEditVM : INotifyPropertyChanged
{
    private string _type = "text";
    public string Type
    {
        get => _type;
        set { _type = value; OnChanged(); OnChanged(nameof(IsChoice)); OnChanged(nameof(IsNumber)); }
    }

    public string Label { get; set; } = "";

    private bool _required;
    public bool Required { get => _required; set { _required = value; OnChanged(); } }

    public string OptionsText { get; set; } = "";
    public string MinText { get; set; } = "";
    public string MaxText { get; set; } = "";

    public bool IsChoice => Type is "single_choice" or "multiple_choice";
    public bool IsNumber => Type == "number";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Una encuesta en el constructor visual.</summary>
public class SurveyEditVM : INotifyPropertyChanged
{
    public string Id { get; set; } = "";

    private string _title = "";
    public string Title { get => _title; set { _title = value; OnChanged(); } }

    public string Description { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";

    public ObservableCollection<QuestionEditVM> Questions { get; } = new();

    public Survey ToSurvey()
    {
        var id = string.IsNullOrWhiteSpace(Id) ? NewId() : Id.Trim();
        Schedule? schedule = (string.IsNullOrWhiteSpace(StartTime) && string.IsNullOrWhiteSpace(EndTime))
            ? null
            : new Schedule { StartTime = NullIfEmpty(StartTime), EndTime = NullIfEmpty(EndTime), Timezone = "America/Bogota" };

        return new Survey
        {
            Id = id,
            Version = 1,
            Title = Title.Trim(),
            Description = NullIfEmpty(Description),
            Schedule = schedule,
            Questions = Questions.Select((q, i) => new Question
            {
                Id = "q" + (i + 1),
                Type = q.Type,
                Label = q.Label.Trim(),
                Required = q.Required,
                Options = q.IsChoice
                    ? q.OptionsText.Split(',').Select(o => o.Trim()).Where(o => o.Length > 0).ToList()
                    : null,
                Min = int.TryParse(q.MinText, out var mn) ? mn : (int?)null,
                Max = int.TryParse(q.MaxText, out var mx) ? mx : (int?)null,
            }).ToList()
        };
    }

    public static SurveyEditVM FromSurvey(Survey s)
    {
        var vm = new SurveyEditVM
        {
            Id = s.Id,
            Title = s.Title,
            Description = s.Description ?? "",
            StartTime = s.Schedule?.StartTime ?? "",
            EndTime = s.Schedule?.EndTime ?? ""
        };
        foreach (var q in s.Questions)
        {
            vm.Questions.Add(new QuestionEditVM
            {
                Type = q.Type,
                Label = q.Label,
                Required = q.Required,
                OptionsText = q.Options != null ? string.Join(", ", q.Options) : "",
                MinText = q.Min?.ToString() ?? "",
                MaxText = q.Max?.ToString() ?? ""
            });
        }
        return vm;
    }

    /// <summary>Plantilla de ejemplo con TODOS los tipos de pregunta.</summary>
    public static SurveyEditVM PlantillaCompleta()
    {
        var vm = new SurveyEditVM
        {
            Id = "survey-demo-001",
            Title = "Plantilla completa (ejemplo)",
            Description = "Ejemplo con todos los tipos de pregunta. Clónala para crear nuevas encuestas.",
            StartTime = "06:00",
            EndTime = "20:00"
        };
        vm.Questions.Add(new QuestionEditVM { Type = "text", Label = "Nombre completo del encuestado", Required = true });
        vm.Questions.Add(new QuestionEditVM { Type = "single_choice", Label = "Estrato socioeconómico", Required = true, OptionsText = "1, 2, 3, 4, 5, 6" });
        vm.Questions.Add(new QuestionEditVM { Type = "multiple_choice", Label = "Servicios públicos disponibles", OptionsText = "Agua, Luz, Gas, Internet, Alcantarillado" });
        vm.Questions.Add(new QuestionEditVM { Type = "number", Label = "Número de personas en el hogar", Required = true, MinText = "1", MaxText = "30" });
        vm.Questions.Add(new QuestionEditVM { Type = "date", Label = "Fecha de nacimiento del jefe de hogar", Required = true });
        vm.Questions.Add(new QuestionEditVM { Type = "image", Label = "Foto de la fachada de la vivienda", Required = true });
        vm.Questions.Add(new QuestionEditVM { Type = "gps", Label = "Ubicación de la vivienda", Required = true });
        return vm;
    }

    public static string NewId() => "survey-" + Guid.NewGuid().ToString("N")[..8];

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Estado del editor: lista de encuestas + selección actual.</summary>
public class EditorViewModel : INotifyPropertyChanged
{
    public ObservableCollection<SurveyEditVM> Surveys { get; } = new();

    private SurveyEditVM? _selected;
    public SurveyEditVM? Selected { get => _selected; set { _selected = value; OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
