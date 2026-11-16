using System.Net;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class AssistantRequestLimiter(TimeProvider timeProvider)
{
    const int ClientLimit = 5;
    const int AppLimit = 60;
    static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // Both limits share this queue so a rejected request never consumes either allowance.
    readonly Queue<(IPAddress? Client, long AcceptedAt)> _requests = new();

    public TimeSpan TryAcquire(IPAddress? client)
    {
        if (client?.IsIPv4MappedToIPv6 == true)
            client = client.MapToIPv4();

        lock (_requests)
        {
            long now = timeProvider.GetTimestamp();
            while (
                _requests.TryPeek(out (IPAddress? Client, long AcceptedAt) oldest)
                && timeProvider.GetElapsedTime(oldest.AcceptedAt, now) >= Window
            )
            {
                _requests.Dequeue();
            }

            TimeSpan retryAfter =
                _requests.Count == AppLimit
                    ? Window - timeProvider.GetElapsedTime(_requests.Peek().AcceptedAt, now)
                    : TimeSpan.Zero;

            int clientRequests = 0;
            long oldestClientRequest = 0;
            foreach ((IPAddress? requestClient, long acceptedAt) in _requests)
            {
                if (!Equals(requestClient, client))
                    continue;

                if (++clientRequests == 1)
                    oldestClientRequest = acceptedAt;
                if (clientRequests == ClientLimit)
                {
                    TimeSpan clientRetryAfter =
                        Window - timeProvider.GetElapsedTime(oldestClientRequest, now);
                    if (clientRetryAfter > retryAfter)
                        retryAfter = clientRetryAfter;
                    break;
                }
            }

            if (retryAfter == TimeSpan.Zero)
                _requests.Enqueue((client, now));

            return retryAfter;
        }
    }
}
