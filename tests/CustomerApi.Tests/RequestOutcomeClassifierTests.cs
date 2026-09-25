namespace CustomerApi.Tests;

public sealed class RequestOutcomeClassifierTests
{
    [Theory]
    [InlineData(400, "validation_failure")]
    [InlineData(401, "authentication_failure")]
    [InlineData(403, "authorization_failure")]
    [InlineData(409, "duplicate_email_conflict")]
    [InlineData(500, "unhandled_failure")]
    [InlineData(503, "unhandled_failure")]
    [InlineData(200, "success_or_other")]
    public void Classify_ReturnsExpectedOutcome(int statusCode, string expectedOutcome)
    {
        var outcome = RequestOutcomeClassifier.Classify(statusCode);

        Assert.Equal(expectedOutcome, outcome);
    }
}
