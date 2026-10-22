namespace VtuberBot.Core;

/// <summary>
/// Bounded in-memory rate limit for commands that can publish a public chat reply.
/// The caller supplies UTC timestamps so cooldown behavior is deterministic in tests.
/// </summary>
public sealed class CommunityCommandRateLimiter
{
    private readonly object gate = new();
    private readonly Dictionary<string, DateTimeOffset> lastByUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<DateTimeOffset> recentReplies = new();
    private readonly int maximumPerMinute;
    private readonly TimeSpan perUserCooldown;
    private readonly TimeSpan minimumGlobalInterval;
    private DateTimeOffset lastGlobalReplyAt = DateTimeOffset.MinValue;

    public CommunityCommandRateLimiter(
        int maximumPerMinute = 8,
        TimeSpan? perUserCooldown = null,
        TimeSpan? minimumGlobalInterval = null)
    {
        this.maximumPerMinute = Math.Clamp(maximumPerMinute, 1, 60);
        this.perUserCooldown = perUserCooldown ?? TimeSpan.FromSeconds(5);
        this.minimumGlobalInterval = minimumGlobalInterval ?? TimeSpan.FromMilliseconds(750);
        if (this.perUserCooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(perUserCooldown));
        if (this.minimumGlobalInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumGlobalInterval));
    }

    public bool TryAcquire(string command, string username, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Length > 32 ||
            string.IsNullOrWhiteSpace(username) || username.Length > 100 ||
            command.Any(char.IsControl) || username.Any(char.IsControl))
            return false;

        var userKey = username.Trim();
        lock (gate)
        {
            while (recentReplies.Count > 0 && now - recentReplies.Peek() >= TimeSpan.FromMinutes(1))
                recentReplies.Dequeue();

            foreach (var expired in lastByUser
                .Where(pair => now - pair.Value >= TimeSpan.FromMinutes(10))
                .Select(pair => pair.Key).ToArray())
                lastByUser.Remove(expired);

            if (recentReplies.Count >= maximumPerMinute ||
                now - lastGlobalReplyAt < minimumGlobalInterval ||
                lastByUser.TryGetValue(userKey, out var lastUser) && now - lastUser < perUserCooldown)
                return false;

            // Bound memory even if chat receives a continuous stream of unique account names.
            if (lastByUser.Count >= 5000)
            {
                foreach (var oldest in lastByUser.OrderBy(pair => pair.Value)
                    .Take(lastByUser.Count - 4000).Select(pair => pair.Key).ToArray())
                    lastByUser.Remove(oldest);
            }

            lastByUser[userKey] = now;
            recentReplies.Enqueue(now);
            lastGlobalReplyAt = now;
            return true;
        }
    }
}
