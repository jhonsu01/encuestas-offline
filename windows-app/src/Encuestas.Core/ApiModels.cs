namespace Encuestas.Core;

public class ResponseDto
{
    public string SurveyId { get; set; } = "";
    public string SurveyorId { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public Dictionary<string, string> Answers { get; set; } = new();
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ImageBase64 { get; set; }
    public string Signature { get; set; } = "";
}

public class LocationEventDto
{
    public string Type { get; set; } = "";        // login, logout, sync
    public string Timestamp { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class SyncBatch
{
    public string Pin { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string SurveyorDocument { get; set; } = "";
    public string SurveyorName { get; set; } = "";
    public List<ResponseDto> Responses { get; set; } = new();
    public List<LocationEventDto> ActivityEvents { get; set; } = new();
}

public class SyncResult
{
    public int Accepted { get; set; }
    public int Duplicates { get; set; }
    public int Rejected { get; set; }
    public string? BatchId { get; set; }
}

public class AuthRequest
{
    public string Pin { get; set; } = "";
    public string DeviceId { get; set; } = "";
}

public class AuthResponse
{
    public string Token { get; set; } = "";
    public int ExpiresIn { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = "ok";
    public string Service { get; set; } = "ENCUESTAS";
    public string Version { get; set; } = "";
    public string Time { get; set; } = "";
}
