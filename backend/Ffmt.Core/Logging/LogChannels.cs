namespace Ffmt.Core.Logging;

// Each enabled channel gets its own rolling file under LoggingOptions.LogDirectory.
public static class LogChannels
{
    public const string Error = "ERROR";
    public const string ScyllaDb = "SCYLLA_DB";
    public const string ScyllaSales = "SCYLLA_SALES";
    public const string ScyllaSalesError = "SCYLLA_SALES_ERROR";
    public const string ScyllaGilflux = "SCYLLA_GILFLUX";
    public const string UniversalisApi = "UNIVERSALIS_API";
    public const string ApiInfo = "API_INFO";
    public const string ApiError = "API_ERROR";
    public const string DbUpdateActivations = "DB_UPDATE_ACTIVATIONS";
    public const string ItemScore = "ITEM_SCORE";

    public const string ContextPropertyName = "Channel";
}
