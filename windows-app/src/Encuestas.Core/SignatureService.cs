using System.Security.Cryptography;
using System.Text;

namespace Encuestas.Core;

/// <summary>
/// Firma de integridad SHA256. Idéntica a FirmaEncuesta.kt (Android):
///   signature = SHA256( surveyId + surveyorId + timestamp + serialize(answers) )
///   serialize(answers) = "clave=valor" ordenados por clave (ordinal), unidos por ";"
/// </summary>
public static class SignatureService
{
    public static string SerializeAnswers(IDictionary<string, string> answers)
    {
        var ordered = answers.OrderBy(kv => kv.Key, StringComparer.Ordinal);
        return string.Join(";", ordered.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    public static string Firmar(string surveyId, string surveyorId, string timestamp, IDictionary<string, string> answers)
    {
        var payload = surveyId + surveyorId + timestamp + SerializeAnswers(answers);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
