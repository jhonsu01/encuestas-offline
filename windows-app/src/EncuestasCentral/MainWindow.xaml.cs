using Encuestas.Core;
using EncuestasCentral.Api;
using EncuestasCentral.Data;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace EncuestasCentral;

public partial class MainWindow : Window
{
    private const int ServicePort = 5000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly ServerState _state = new();
    private readonly ApiHost _api;
    private readonly Discovery.DiscoveryService _discovery = new(ServicePort);
    private readonly EditorViewModel _editorVM = new();

    public MainWindow()
    {
        InitializeComponent();
        _api = new ApiHost(_state);
        _state.OnLog += OnLog;
        EditorRoot.DataContext = _editorVM;
        LoadSurveysIntoEditor();
        UpdatePublishedCount();
    }

    private void OnLog(string message)
    {
        Dispatcher.Invoke(() => LogList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}"));
    }

    // ---------------- Carga / seeding ----------------

    private void LoadSurveysIntoEditor()
    {
        try
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
            var rows = db.Surveys.ToList();

            if (rows.Count == 0)
            {
                // Semilla: plantilla de ejemplo con todos los tipos de pregunta.
                var plantilla = SurveyEditVM.PlantillaCompleta();
                var survey = plantilla.ToSurvey();
                plantilla.Id = survey.Id;
                db.Surveys.Add(new SurveyRow
                {
                    Id = survey.Id,
                    Version = survey.Version,
                    Title = survey.Title,
                    Json = JsonSerializer.Serialize(survey, JsonOpts)
                });
                db.SaveChanges();
                _editorVM.Surveys.Add(plantilla);
                _state.PublishSurvey(survey);
            }
            else
            {
                foreach (var row in rows)
                {
                    var survey = JsonSerializer.Deserialize<Survey>(row.Json, JsonOpts);
                    if (survey == null) continue;
                    _editorVM.Surveys.Add(SurveyEditVM.FromSurvey(survey));
                    _state.PublishSurvey(survey);
                }
            }

            _editorVM.Selected = _editorVM.Surveys.FirstOrDefault();
        }
        catch (Exception ex)
        {
            OnLog($"No se pudieron cargar encuestas: {ex.Message}");
        }
    }

    private void UpdatePublishedCount()
    {
        PublishedCount.Text = $"Publicadas y servidas: {_state.Surveys.Count}";
    }

    // ---------------- Servidor ----------------

    private void StartServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _ = _api.StartAsync(ServicePort);
            _discovery.Start();
            StatusText.Text = $"Servidor ACTIVO en http://0.0.0.0:{ServicePort}  |  Broadcast UDP 8888";
            OnLog("Servidor iniciado.");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error al iniciar: {ex.Message}";
        }
    }

    private async void StopServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _discovery.Stop();
            await _api.StopAsync();
            StatusText.Text = "Servidor detenido.";
            OnLog("Servidor detenido.");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error al detener: {ex.Message}";
        }
    }

    // ---------------- Editor visual ----------------

    private void NuevaEncuesta_Click(object sender, RoutedEventArgs e)
    {
        var vm = new SurveyEditVM { Id = SurveyEditVM.NewId(), Title = "Nueva encuesta" };
        _editorVM.Surveys.Add(vm);
        _editorVM.Selected = vm;
    }

    private void ClonarEncuesta_Click(object sender, RoutedEventArgs e)
    {
        var src = _editorVM.Selected;
        if (src == null) { MessageBox.Show("Selecciona una encuesta para clonar."); return; }

        var copy = SurveyEditVM.FromSurvey(src.ToSurvey());
        copy.Id = SurveyEditVM.NewId();
        copy.Title = src.Title + " (copia)";
        _editorVM.Surveys.Add(copy);
        _editorVM.Selected = copy;
        OnLog($"Encuesta clonada desde '{src.Title}'. Edita y guarda.");
    }

    private void EliminarEncuesta_Click(object sender, RoutedEventArgs e)
    {
        var sel = _editorVM.Selected;
        if (sel == null) return;
        if (MessageBox.Show($"¿Eliminar la encuesta '{sel.Title}'?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        using (var db = new AppDbContext())
        {
            var row = db.Surveys.Find(sel.Id);
            if (row != null) { db.Surveys.Remove(row); db.SaveChanges(); }
        }
        _state.Surveys.RemoveAll(s => s.Id == sel.Id);
        _editorVM.Surveys.Remove(sel);
        _editorVM.Selected = _editorVM.Surveys.FirstOrDefault();
        UpdatePublishedCount();
        OnLog("Encuesta eliminada.");
    }

    private void AnadirPregunta_Click(object sender, RoutedEventArgs e)
    {
        var sel = _editorVM.Selected;
        if (sel == null) { MessageBox.Show("Primero crea o selecciona una encuesta."); return; }
        var code = (NewTypeCombo.SelectedValue as string) ?? "text";
        sel.Questions.Add(new QuestionEditVM { Type = code, Label = "" });
    }

    private void EliminarPregunta_Click(object sender, RoutedEventArgs e)
    {
        var sel = _editorVM.Selected;
        if (sel != null && sender is FrameworkElement fe && fe.DataContext is QuestionEditVM q)
            sel.Questions.Remove(q);
    }

    private void SubirPregunta_Click(object sender, RoutedEventArgs e)
    {
        var sel = _editorVM.Selected;
        if (sel != null && sender is FrameworkElement fe && fe.DataContext is QuestionEditVM q)
        {
            int i = sel.Questions.IndexOf(q);
            if (i > 0) sel.Questions.Move(i, i - 1);
        }
    }

    private void BajarPregunta_Click(object sender, RoutedEventArgs e)
    {
        var sel = _editorVM.Selected;
        if (sel != null && sender is FrameworkElement fe && fe.DataContext is QuestionEditVM q)
        {
            int i = sel.Questions.IndexOf(q);
            if (i >= 0 && i < sel.Questions.Count - 1) sel.Questions.Move(i, i + 1);
        }
    }

    private void GuardarEncuesta_Click(object sender, RoutedEventArgs e)
    {
        var sel = _editorVM.Selected;
        if (sel == null) return;
        if (string.IsNullOrWhiteSpace(sel.Title))
        {
            MessageBox.Show("La encuesta necesita un título.", "Guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var survey = sel.ToSurvey();
        sel.Id = survey.Id;
        _state.PublishSurvey(survey);

        try
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
            var json = JsonSerializer.Serialize(survey, JsonOpts);
            var existing = db.Surveys.Find(survey.Id);
            if (existing == null)
            {
                db.Surveys.Add(new SurveyRow { Id = survey.Id, Version = survey.Version, Title = survey.Title, Json = json });
            }
            else
            {
                existing.Version = survey.Version;
                existing.Title = survey.Title;
                existing.Json = json;
            }
            db.SaveChanges();

            UpdatePublishedCount();
            OnLog($"Encuesta publicada: {survey.Title} ({survey.Questions.Count} preguntas)");
            MessageBox.Show("Encuesta guardada y publicada. Los dispositivos ya pueden descargarla.",
                "Guardar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Guardar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Exportar_Click(object sender, RoutedEventArgs e)
    {
        var sel = _editorVM.Selected;
        if (sel == null) return;
        var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = $"{sel.Id}.json" };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(sel.ToSurvey(), JsonOpts));
            OnLog($"Exportado a {dlg.FileName}");
        }
    }

    // ---------------- Encuestadores ----------------

    private void RefreshSurveyors_Click(object sender, RoutedEventArgs e)
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        SurveyorsGrid.ItemsSource = db.Surveyors.OrderByDescending(s => s.ResponseCount).ToList();
    }

    // ---------------- Dashboard ----------------

    private void RefreshDashboard_Click(object sender, RoutedEventArgs e)
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        var responses = db.Responses.OrderByDescending(r => r.ReceivedAt).ToList();
        ResponsesGrid.ItemsSource = responses;
        LblTotal.Text = $"Total de respuestas: {responses.Count}";

        var bySurveyor = responses
            .GroupBy(r => r.SurveyorId)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();
        LblBySurveyor.Text = bySurveyor.Count == 0
            ? "Sin datos por encuestador."
            : "Por encuestador → " + string.Join("   |   ", bySurveyor);
    }
}
