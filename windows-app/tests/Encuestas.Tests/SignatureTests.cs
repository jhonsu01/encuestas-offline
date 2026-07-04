using Encuestas.Core;
using Xunit;

namespace Encuestas.Tests;

public class SignatureTests
{
    [Fact]
    public void SerializeAnswers_OrdenaPorClave()
    {
        var s = SignatureService.SerializeAnswers(new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        Assert.Equal("a=1;b=2", s);
    }

    [Fact]
    public void Firma_CoincideConAndroid()
    {
        // Mismo vector que FirmaEncuestaTest.kt -> DEBE dar el mismo hash.
        var hash = SignatureService.Firmar("s1", "CC-1", "2026-01-01T00:00:00Z",
            new Dictionary<string, string> { ["q1"] = "hola" });
        Assert.Equal("91a136ffc85ac4b459ade86e2b1397b2a7e9998b3ec58ad781568d021eda5793", hash);
    }

    [Fact]
    public void VerifySignature_RoundTrip()
    {
        var answers = new Dictionary<string, string> { ["q1"] = "x", ["q2"] = "y" };
        var sig = SignatureService.Firmar("s1", "CC-1", "t", answers);
        var dto = new ResponseDto { SurveyId = "s1", SurveyorId = "CC-1", Timestamp = "t", Answers = answers, Signature = sig };
        Assert.True(BatchValidator.VerifySignature(dto));
    }

    [Fact]
    public void BatchIsValid_DetectaFirmaMala()
    {
        var answers = new Dictionary<string, string> { ["q1"] = "x" };
        var good = new ResponseDto { SurveyId = "s1", SurveyorId = "CC-1", Timestamp = "t", Answers = answers,
            Signature = SignatureService.Firmar("s1", "CC-1", "t", answers) };
        var bad = new ResponseDto { SurveyId = "s2", SurveyorId = "CC-1", Timestamp = "t", Answers = answers,
            Signature = "deadbeef" };
        var batch = new SyncBatch { Responses = { good, bad } };
        Assert.False(BatchValidator.BatchIsValid(batch, out var invalid));
        Assert.Equal(1, invalid);
    }
}
