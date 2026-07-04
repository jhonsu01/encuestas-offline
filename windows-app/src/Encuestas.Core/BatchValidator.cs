namespace Encuestas.Core;

/// <summary>Validación de integridad de un lote de respuestas.</summary>
public static class BatchValidator
{
    public static bool VerifySignature(ResponseDto r)
    {
        var expected = SignatureService.Firmar(r.SurveyId, r.SurveyorId, r.Timestamp, r.Answers);
        return string.Equals(expected, r.Signature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Un lote es válido solo si TODAS las firmas son correctas (lote completo/transaccional).
    /// Los duplicados no invalidan el lote: se filtran en la persistencia.
    /// </summary>
    public static bool BatchIsValid(SyncBatch batch, out int invalidCount)
    {
        invalidCount = batch.Responses.Count(r => !VerifySignature(r));
        return invalidCount == 0;
    }
}
