using System.ComponentModel.DataAnnotations;

namespace EncuestasCentral.Data;

public class SurveyRow
{
    [Key] public string Id { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public string Json { get; set; } = "";
}

public class SurveyorRow
{
    [Key] public string Id { get; set; } = "";
    public string? FullName { get; set; }
    public string FirstSeen { get; set; } = "";
    public int ResponseCount { get; set; }
}

public class ResponseRow
{
    [Key] public string Signature { get; set; } = "";
    public string SurveyId { get; set; } = "";
    public string SurveyorId { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string AnswersJson { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ImageBase64 { get; set; }
    public string? BatchPin { get; set; }
    public string? BatchId { get; set; }
    public string ReceivedAt { get; set; } = "";
}

public class DeviceRow
{
    [Key] public string DeviceId { get; set; } = "";
    public string SurveyorId { get; set; } = "";
    public string FirstSeen { get; set; } = "";
    public bool Allowed { get; set; } = true;
}

public class BatchRow
{
    [Key] public string BatchId { get; set; } = "";
    public string Pin { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string SurveyorId { get; set; } = "";
    public int Accepted { get; set; }
    public int Duplicates { get; set; }
    public int Rejected { get; set; }
    public string ReceivedAt { get; set; } = "";
}

public class LogRow
{
    [Key] public int Id { get; set; }
    public string Time { get; set; } = "";
    public string Message { get; set; } = "";
}

public class LocationEventRow
{
    [Key] public int Id { get; set; }
    public string SurveyorId { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Type { get; set; } = "";        // login, logout, sync
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Timestamp { get; set; } = "";
    public string ReceivedAt { get; set; } = "";
}
