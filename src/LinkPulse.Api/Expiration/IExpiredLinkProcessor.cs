namespace LinkPulse.Api.Expiration;

public interface IExpiredLinkProcessor
{
    Task<int> ProcessAsync(
        DateTimeOffset currentTime,
        CancellationToken cancellationToken);
}