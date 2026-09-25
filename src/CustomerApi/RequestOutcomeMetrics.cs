using System.Diagnostics.Metrics;

namespace CustomerApi;

public static class RequestOutcomeMetrics
{
    public const string MeterName = "CustomerApi.RequestOutcomes";
    public const string CounterName = "customerapi_request_outcomes_total";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestOutcomeCounter = Meter.CreateCounter<long>(
        CounterName,
        unit: "requests",
        description: "Count of customer API request outcomes by endpoint and status code.");

    public static void Record(string outcome, string endpoint, int statusCode) =>
        RequestOutcomeCounter.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("status_code", statusCode));
}