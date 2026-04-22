namespace IsoDoc.Infrastructure.Search;

/// <summary>
/// Thrown when an Elasticsearch search request fails so callers can fall back to SQL.
/// </summary>
public sealed class ElasticsearchSearchException : Exception
{
    public ElasticsearchSearchException(string message) : base(message)
    {
    }

    public ElasticsearchSearchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
