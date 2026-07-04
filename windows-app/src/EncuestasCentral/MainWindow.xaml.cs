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

    public MainWindow()
    {
        InitializeComponent();
        _api = new ApiHost(_state);
        _state.OnLog += OnLog;
        EditorJson.Text = SampleData.ExampleJson;
        LoadSurveysFromDb();
        UpdatePublishedCount();
    }

    private void OnLog(string message)
    {
        Dispatcher.Invoke(() => LogList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}"));
    }

    private void LoadSurveysFromDb()
    {
        try
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
            foreach (var row in db.Surveys.ToList())
            {
                var survey = JsonSerializer.Deserialize<Survey>(row.Json, JsonOpts);
                if (survey != null) _state.PublishSurvey(survey);
            }
        }
        catch (Exception ex)
        {
            OnLog($"No se pudieron cargar encuestas: {ex.Message}");
        }
    }

    private void UpdatePublishedCount()
    {
        PublishedCount.Text = $"Publicadas: {_state.Surveys.Count}";
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

    // ---------------- Editor ----------------

    private void CargarEjemplo_Click(object sender, RoutedEventArgs e)
    {
        EditorJson.Text = SampleData.ExampleJson;
    }

    private void Publicar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var survey = JsonSerializer.Deserialize<Survey>(EditorJson.Text, JsonOpts);
            if (survey == null || string.IsNullOrWhiteSpace(survey.Id))
            {
                MessageBox.Show("El JSON no tiene un 'id' válido.", "Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _state.PublishSurvey(survey);

            using var db = new AppDbContext();
            db.Database.EnsureCreated();
            var existing = db.Surveys.Find(survey.Id);
            if (existing == null)
            {
                db.Surveys.Add(new SurveyRow { Id = survey.Id, Version = survey.Version, Title = survey.Title, Json = EditorJson.Text });
            }
            else
            {
                existing.Version = survey.Version;
                existing.Title = survey.Title;
                existing.Json = EditorJson.Text;
            }
            db.SaveChanges();

            UpdatePublishedCount();
            OnLog($"Encuesta publicada: {survey.Title} ({survey.Questions.Count} preguntas)");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"JSON inválido: {ex.Message}", "Editor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Exportar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "encuesta.json" };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, EditorJson.Text);
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
