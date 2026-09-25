namespace CustomerApi;

public static class RequestOutcomeClassifier
{
    public static string Classify(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "validation_failure",
        StatusCodes.Status401Unauthorized => "authentication_failure",
        StatusCodes.Status403Forbidden => "authorization_failure",
        StatusCodes.Status409Conflict => "duplicate_email_conflict",
        >= StatusCodes.Status500InternalServerError => "unhandled_failure",
        _ => "success_or_other"
    };
}
