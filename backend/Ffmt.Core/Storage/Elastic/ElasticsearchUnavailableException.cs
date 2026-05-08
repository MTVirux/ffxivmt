namespace Ffmt.Core.Storage.Elastic;

public sealed class ElasticsearchUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
