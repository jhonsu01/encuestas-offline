using Encuestas.Core;
using EncuestasCentral.Api;
using EncuestasCentral.Data;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        _discovery.OnDeviceSeen += OnDeviceSeen;
        EditorRoot.DataContext = _editorVM;
        LoadSurveysIntoEditor();
        UpdatePublishedCount();
    }

    private void OnLog(string message)
    {
        Dispatcher.Invoke(() => LogList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}"));
    }

    private void OnDeviceSeen(Discovery.DeviceSeen dev)
    {
        var id = string.IsNullOrWhiteSpace(dev.DeviceId) ? dev.Ip : dev.DeviceId;
        var label = string.IsNullOrWhiteSpace(dev.Name) ? id : dev.Name;
        Dispatcher.Invoke(() =>
        {
            OnLog($"📶 Dispositivo en red: {label} ({dev.Ip})" +
                  (string.IsNullOrWhiteSpace(dev.SurveyorId) ? "" : $" — {dev.SurveyorId}"));
            try
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();
                var now = DateTime.UtcNow.ToString("o");

                if (db.Devices.Find(id) == null)
                {
                    db.Devices.Add(new DeviceRow
                    {
                        DeviceId = id,
                        SurveyorId = dev.SurveyorId,
                        FirstSeen = now,
                        Allowed = true
                    });
                }

                // Registrar/actualizar el nombre del encuestador aunque aún no haya sincronizado.
                if (!string.IsNullOrWhiteSpace(dev.SurveyorId))
                {
                    var sr = db.Surveyors.Find(dev.SurveyorId);
                    if (sr == null)
                    {
                        db.Surveyors.Add(new SurveyorRow
                        {
                            Id = dev.SurveyorId,
                            FullName = string.IsNullOrWhiteSpace(dev.Name) ? null : dev.Name,
                            FirstSeen = now,
                            ResponseCount = 0
                        });
                    }
                    else if (string.IsNullOrWhiteSpace(sr.FullName) && !string.IsNullOrWhiteSpace(dev.Name))
                    {
                        sr.FullName = dev.Name;
                    }
                }

                db.SaveChanges();
            }
            catch { /* mejor esfuerzo */ }
        });
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
            var ips = string.Join(", ", Discovery.DiscoveryService.LocalIPv4());
            StatusText.Text = $"Servidor ACTIVO  |  IPs del equipo: {ips}  |  puerto {ServicePort}  |  discovery UDP 8888";
            OnLog($"Servidor iniciado. IPs del equipo: {ips} (si el teléfono no autodetecta, escribe una de estas en 'IP manual').");
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

    private void ImportarEncuesta_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json|Todos los archivos (*.*)|*.*",
            Title = "Importar encuesta desde JSON"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var survey = JsonSerializer.Deserialize<Survey>(json, JsonOpts);
            if (survey == null || string.IsNullOrWhiteSpace(survey.Title))
            {
                MessageBox.Show("El archivo no contiene una encuesta válida (falta el título).",
                    "Importar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Nuevo id para no colisionar con encuestas existentes al guardar.
            var vm = SurveyEditVM.FromSurvey(survey);
            vm.Id = SurveyEditVM.NewId();
            _editorVM.Surveys.Add(vm);
            _editorVM.Selected = vm;
            OnLog($"Encuesta importada desde {dlg.FileName}: '{vm.Title}' ({vm.Questions.Count} preguntas). Guarda y publica para confirmar.");
            MessageBox.Show(
                $"Encuesta '{vm.Title}' importada con {vm.Questions.Count} pregunta(s).\n\n" +
                "Revísala y pulsa «Guardar y publicar» para que los dispositivos puedan descargarla.",
                "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo leer el JSON:\n{ex.Message}",
                "Importar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    // ---------------- Dispositivos en red ----------------

    private void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        DevicesGrid.ItemsSource = db.Devices.OrderByDescending(d => d.FirstSeen).ToList();
    }

    // ---------------- Dashboard ----------------

    /// <summary>Respuesta enriquecida para mostrar/exportar en el dashboard.</summary>
    private sealed class DashboardRow
    {
        public string SurveyId { get; set; } = "";
        public string SurveyTitle { get; set; } = "";
        public string SurveyorId { get; set; } = "";
        public string ReceivedAt { get; set; } = "";
        public string ReceivedDate { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string BatchPin { get; set; } = "";
        public string Latitude { get; set; } = "";
        public string Longitude { get; set; } = "";
        public string HasImage { get; set; } = "";
        public string Signature { get; set; } = "";
    }

    private static string CsvEscape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private List<DashboardRow> BuildDashboardRows()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();

        // Materializar primero (LINQ-to-Objects) para poder enriquecer con título/fecha en memoria.
        var titles = db.Surveys.ToDictionary(s => s.Id, s => s.Title);
        var raw = db.Responses
            .OrderByDescending(r => r.ReceivedAt)
            .ToList();

        var rows = raw.Select(r => new DashboardRow
        {
            SurveyId = r.SurveyId,
            SurveyTitle = titles.TryGetValue(r.SurveyId, out var t) ? t : r.SurveyId,
            SurveyorId = r.SurveyorId,
            ReceivedAt = r.ReceivedAt,
            ReceivedDate = SafeDate(r.ReceivedAt),
            Timestamp = r.Timestamp,
            BatchPin = r.BatchPin ?? "",
            Latitude = r.Latitude?.ToString(CultureInfo.InvariantCulture) ?? "",
            Longitude = r.Longitude?.ToString(CultureInfo.InvariantCulture) ?? "",
            HasImage = string.IsNullOrEmpty(r.ImageBase64) ? "no" : "sí",
            Signature = r.Signature
        }).ToList();
        return rows;
    }

    private static string SafeDate(string isoTimestamp)
    {
        // ISO-8601 llega con 'T' o con espacio; tomamos solo la parte de fecha.
        if (string.IsNullOrWhiteSpace(isoTimestamp)) return "";
        var space = isoTimestamp.IndexOf('T');
        if (space > 0) return isoTimestamp[..space];
        space = isoTimestamp.IndexOf(' ');
        return space > 0 ? isoTimestamp[..space] : isoTimestamp;
    }

    private List<DashboardRow> _allRows = new();
    private List<DashboardRow> _filteredRows = new();
    private bool _suppressFilter;

    private void RefreshDashboard_Click(object sender, RoutedEventArgs e) => RefreshDashboardData();

    private void RefreshDashboardData()
    {
        _allRows = BuildDashboardRows();
        PopulateFilterCombos();
        ApplyFilters();
    }

    private void PopulateFilterCombos()
    {
        _suppressFilter = true;

        var prevEnc = EncuestaFilterCombo.SelectedItem as string;
        EncuestaFilterCombo.Items.Clear();
        EncuestaFilterCombo.Items.Add("Todas");
        foreach (var t in _allRows.Select(r => r.SurveyTitle).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x))
            EncuestaFilterCombo.Items.Add(t);
        EncuestaFilterCombo.SelectedItem =
            (prevEnc != null && EncuestaFilterCombo.Items.Contains(prevEnc)) ? prevEnc : "Todas";

        var prevSurv = EncuestadorFilterCombo.SelectedItem as string;
        EncuestadorFilterCombo.Items.Clear();
        EncuestadorFilterCombo.Items.Add("Todos");
        foreach (var s in _allRows.Select(r => r.SurveyorId).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x))
            EncuestadorFilterCombo.Items.Add(s);
        EncuestadorFilterCombo.SelectedItem =
            (prevSurv != null && EncuestadorFilterCombo.Items.Contains(prevSurv)) ? prevSurv : "Todos";

        var prevDia = DiaFilterCombo.SelectedItem as string;
        DiaFilterCombo.Items.Clear();
        DiaFilterCombo.Items.Add("Todos");
        foreach (var d in _allRows.Select(r => r.ReceivedDate).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderByDescending(x => x))
            DiaFilterCombo.Items.Add(d);
        DiaFilterCombo.SelectedItem =
            (prevDia != null && DiaFilterCombo.Items.Contains(prevDia)) ? prevDia : "Todos";

        _suppressFilter = false;
    }

    private void Filtro_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressFilter) return;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var enc = EncuestaFilterCombo.SelectedItem as string;
        var surv = EncuestadorFilterCombo.SelectedItem as string;
        var dia = DiaFilterCombo.SelectedItem as string;

        IEnumerable<DashboardRow> q = _allRows;
        if (!string.IsNullOrEmpty(enc) && enc != "Todas") q = q.Where(r => r.SurveyTitle == enc);
        if (!string.IsNullOrEmpty(surv) && surv != "Todos") q = q.Where(r => r.SurveyorId == surv);
        if (!string.IsNullOrEmpty(dia) && dia != "Todos") q = q.Where(r => r.ReceivedDate == dia);
        _filteredRows = q.ToList();

        ResponsesGrid.ItemsSource = _filteredRows;
        LblTotal.Text = $"Mostrando: {_filteredRows.Count} de {_allRows.Count} respuestas";

        var groupKey = GroupByCombo.SelectedIndex switch
        {
            1 => "Encuesta",
            2 => "Encuestador",
            3 => "Fecha (día)",
            _ => "Sin agrupar"
        };

        if (_filteredRows.Count == 0)
        {
            LblBySurveyor.Text = "Sin datos para el filtro actual.";
            return;
        }
        if (groupKey == "Sin agrupar")
        {
            LblBySurveyor.Text = "";
            return;
        }

        IEnumerable<string> summary = groupKey switch
        {
            "Encuesta" => _filteredRows.GroupBy(r => r.SurveyTitle).Select(g => $"{g.Key}: {g.Count()}"),
            "Encuestador" => _filteredRows.GroupBy(r => r.SurveyorId).Select(g => $"{g.Key}: {g.Count()}"),
            "Fecha (día)" => _filteredRows.GroupBy(r => r.ReceivedDate).OrderByDescending(g => g.Key).Select(g => $"{g.Key}: {g.Count()}"),
            _ => Enumerable.Empty<string>()
        };
        LblBySurveyor.Text = $"Agrupado por {groupKey} → " + string.Join("   |   ", summary);
    }

    private List<DashboardRow> EnsureRows()
    {
        if (_allRows.Count == 0) RefreshDashboardData();
        return _filteredRows;
    }

    /// <summary>Carga de BD las respuestas completas (con imagen/answers) del conjunto filtrado.</summary>
    private (List<ResponseRow> responses, Dictionary<string, Survey> surveys, Dictionary<string, string> names)
        LoadFilteredResponses(List<DashboardRow> rows)
    {
        var sigs = rows.Select(r => r.Signature).ToList();
        using var db = new AppDbContext();
        db.Database.EnsureCreated();

        var responses = new List<ResponseRow>();
        foreach (var chunk in sigs.Chunk(400))
        {
            var set = chunk.ToList();
            responses.AddRange(db.Responses.Where(r => set.Contains(r.Signature)).ToList());
        }
        responses = responses.OrderByDescending(r => r.ReceivedAt).ToList();

        var surveys = db.Surveys.ToList()
            .Select(s => JsonSerializer.Deserialize<Survey>(s.Json, JsonOpts))
            .Where(s => s != null)
            .ToDictionary(s => s!.Id, s => s!);
        var names = db.Surveyors.ToList().ToDictionary(s => s.Id, s => s.FullName ?? "");
        return (responses, surveys, names);
    }

    /// <summary>
    /// CSV "ancho": metadatos + una columna por pregunta (con la respuesta real).
    /// Si el filtro deja una sola encuesta, las columnas quedan limpias y ordenadas.
    /// </summary>
    private static string BuildCsv(List<ResponseRow> responses,
        Dictionary<string, Survey> surveys, Dictionary<string, string> surveyorNames)
    {
        // Columnas de respuesta: (surveyId, qId) en orden de aparición de cada encuesta.
        var answerCols = new List<(string surveyId, string qId, string header)>();
        var seen = new HashSet<string>();
        var usedHeaders = new HashSet<string>();
        foreach (var sid in responses.Select(r => r.SurveyId).Distinct())
        {
            if (!surveys.TryGetValue(sid, out var s)) continue;
            foreach (var qq in s.Questions)
            {
                if (!seen.Add(sid + "|" + qq.Id)) continue;
                var header = qq.Label;
                if (!usedHeaders.Add(header))
                {
                    header = $"{qq.Label} ({s.Title})";
                    usedHeaders.Add(header);
                }
                answerCols.Add((sid, qq.Id, header));
            }
        }

        var metaHeaders = new[]
        {
            "encuesta", "encuestador_id", "encuestador_nombre", "fecha", "recibido",
            "latitud", "longitud", "imagen", "lote_pin", "firma"
        };

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", metaHeaders.Concat(answerCols.Select(c => c.header)).Select(CsvEscape)));

        foreach (var r in responses)
        {
            Dictionary<string, string> ans;
            try { ans = JsonSerializer.Deserialize<Dictionary<string, string>>(r.AnswersJson) ?? new(); }
            catch { ans = new(); }

            var surveyTitle = surveys.TryGetValue(r.SurveyId, out var sv) ? sv.Title : r.SurveyId;
            var name = surveyorNames.TryGetValue(r.SurveyorId, out var n) ? n : "";

            var meta = new[]
            {
                surveyTitle, r.SurveyorId, name, SafeDate(r.ReceivedAt), r.ReceivedAt,
                r.Latitude?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.Longitude?.ToString(CultureInfo.InvariantCulture) ?? "",
                string.IsNullOrEmpty(r.ImageBase64) ? "no" : "sí",
                r.BatchPin ?? "", r.Signature
            };

            var answerVals = answerCols.Select(c =>
                c.surveyId == r.SurveyId && ans.TryGetValue(c.qId, out var v) ? v : "");

            sb.AppendLine(string.Join(",", meta.Concat(answerVals).Select(CsvEscape)));
        }

        return sb.ToString();
    }

    private void ExportarCsv_Click(object sender, RoutedEventArgs e)
    {
        var rows = EnsureRows();
        if (rows.Count == 0)
        {
            MessageBox.Show("No hay respuestas para exportar con el filtro actual.",
                "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"encuestas_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var data = LoadFilteredResponses(rows);
            var csv = BuildCsv(data.responses, data.surveys, data.names);

            // BOM UTF-8 para que Excel abra correctamente los acentos.
            File.WriteAllText(dlg.FileName, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            OnLog($"Exportado CSV con respuestas: {data.responses.Count} filas → {dlg.FileName}");
            MessageBox.Show(
                $"Exportadas {data.responses.Count} respuestas (con columnas de cada pregunta) a:\n{dlg.FileName}\n\n" +
                "Consejo: filtra por una sola encuesta para obtener columnas limpias.",
                "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo exportar:\n{ex.Message}",
                "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportarHtml_Click(object sender, RoutedEventArgs e)
    {
        var rows = EnsureRows();
        if (rows.Count == 0)
        {
            MessageBox.Show("No hay respuestas para exportar con el filtro actual.",
                "Exportar HTML", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "HTML (*.html)|*.html",
            FileName = $"encuestas_{DateTime.Now:yyyyMMdd_HHmm}.html"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var data = LoadFilteredResponses(rows);
            var html = BuildHtmlReport(data.responses, data.surveys, data.names);
            File.WriteAllText(dlg.FileName, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            OnLog($"Exportado HTML con imágenes: {data.responses.Count} respuestas → {dlg.FileName}");
            MessageBox.Show(
                $"Exportadas {data.responses.Count} respuestas (con imágenes) a:\n{dlg.FileName}\n\n" +
                "Ábrelo con cualquier navegador; desde ahí puedes imprimirlo a PDF.",
                "Exportar HTML", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo exportar:\n{ex.Message}",
                "Exportar HTML", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BuildHtmlReport(List<ResponseRow> responses,
        Dictionary<string, Survey> surveys, Dictionary<string, string> surveyorNames)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>Encuestas sincronizadas</title><style>");
        sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222;background:#f5f5f5}");
        sb.Append("h1{color:#00695c}h2{color:#00695c;margin:0 0 6px}");
        sb.Append(".card{background:#fff;border:1px solid #ddd;border-radius:8px;padding:16px;margin:0 0 16px;box-shadow:0 1px 3px rgba(0,0,0,.08)}");
        sb.Append(".meta{color:#555;font-size:13px}table{border-collapse:collapse;margin:8px 0;width:100%}");
        sb.Append("th,td{border:1px solid #e0e0e0;padding:6px 10px;text-align:left;vertical-align:top}th{background:#f0f0f0;width:40%}");
        sb.Append("img{max-width:360px;max-height:360px;border-radius:6px;margin-top:8px;border:1px solid #ccc}");
        sb.Append("</style></head><body>");
        sb.Append($"<h1>Encuestas sincronizadas</h1><p class=\"meta\">Total: {responses.Count} · Generado: {HtmlEscape(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))}</p>");

        foreach (var r in responses)
        {
            surveys.TryGetValue(r.SurveyId, out var survey);
            var labels = survey?.Questions.ToDictionary(q => q.Id, q => q.Label) ?? new Dictionary<string, string>();
            var name = surveyorNames.TryGetValue(r.SurveyorId, out var n) && !string.IsNullOrWhiteSpace(n) ? n : r.SurveyorId;

            sb.Append("<div class=\"card\">");
            sb.Append($"<h2>{HtmlEscape(survey?.Title ?? r.SurveyId)}</h2>");
            sb.Append("<p class=\"meta\">");
            sb.Append($"Encuestador: {HtmlEscape(name)} ({HtmlEscape(r.SurveyorId)})<br>");
            sb.Append($"Fecha: {HtmlEscape(r.Timestamp)} · Recibido: {HtmlEscape(r.ReceivedAt)}<br>");
            if (r.Latitude != null && r.Longitude != null)
                sb.Append($"GPS: {r.Latitude?.ToString(CultureInfo.InvariantCulture)}, {r.Longitude?.ToString(CultureInfo.InvariantCulture)}<br>");
            sb.Append($"Firma: {HtmlEscape(r.Signature)}</p>");

            Dictionary<string, string> answers;
            try { answers = JsonSerializer.Deserialize<Dictionary<string, string>>(r.AnswersJson) ?? new(); }
            catch { answers = new(); }

            sb.Append("<table><tr><th>Pregunta</th><th>Respuesta</th></tr>");
            foreach (var kv in answers)
            {
                var label = labels.TryGetValue(kv.Key, out var l) && !string.IsNullOrWhiteSpace(l) ? l : kv.Key;
                sb.Append($"<tr><td>{HtmlEscape(label)}</td><td>{HtmlEscape(kv.Value)}</td></tr>");
            }
            sb.Append("</table>");

            if (!string.IsNullOrEmpty(r.ImageBase64))
                sb.Append($"<div><img src=\"data:image/jpeg;base64,{r.ImageBase64}\" alt=\"foto\"></div>");

            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private void EliminarFiltrados_Click(object sender, RoutedEventArgs e)
    {
        var rows = _filteredRows;
        if (rows.Count == 0)
        {
            MessageBox.Show("No hay respuestas para eliminar con el filtro actual.",
                "Eliminar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(
                $"¿Eliminar {rows.Count} respuesta(s) del filtro actual?\n\n" +
                "Esta acción NO se puede deshacer.\nSugerencia: exporta antes (CSV o HTML).",
                "Eliminar registros", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            var sigs = rows.Select(r => r.Signature).ToList();
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
            int deleted = 0;
            foreach (var chunk in sigs.Chunk(400))
            {
                var set = chunk.ToList();
                var del = db.Responses.Where(r => set.Contains(r.Signature)).ToList();
                db.Responses.RemoveRange(del);
                deleted += del.Count;
            }
            db.SaveChanges();
            RecomputeSurveyorCounts(db);

            OnLog($"Eliminadas {deleted} respuestas del dashboard.");
            MessageBox.Show($"Eliminadas {deleted} respuesta(s).", "Eliminar", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshDashboardData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo eliminar:\n{ex.Message}", "Eliminar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void RecomputeSurveyorCounts(AppDbContext db)
    {
        foreach (var s in db.Surveyors.ToList())
            s.ResponseCount = db.Responses.Count(r => r.SurveyorId == s.Id);
        db.SaveChanges();
    }

    private static string HtmlEscape(string? s) =>
        string.IsNullOrEmpty(s) ? "" :
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
