namespace Companion.Core;

public sealed class VoteEngine
{
    public sealed record VoteSession(string Reason, HashSet<string> Candidates, DateTimeOffset Deadline);

    private VoteSession? _active;
    private readonly Dictionary<string,string> _votes = new(); // voter -> candidate

    public bool HasActive => _active is not null;
    public VoteSession? Active => _active;

    public void Begin(string reason, IEnumerable<string> candidates, TimeSpan duration)
    {
        _active = new VoteSession(reason, candidates.ToHashSet(), DateTimeOffset.UtcNow.Add(duration));
        _votes.Clear();
    }

    public bool Cast(string voter, string candidate)
    {
        if (_active is null) return false;
        if (!_active.Candidates.Contains(candidate)) return false;
        _votes[voter] = candidate;
        return true;
    }

    public (string winner, Dictionary<string,int> tally)? TryResolve(HashSet<string> eligibleVoters)
    {
        if (_active is null) return null;
        if (DateTimeOffset.UtcNow < _active.Deadline && _votes.Keys.Intersect(eligibleVoters).Count() < eligibleVoters.Count)
            return null;

        var tally = new Dictionary<string,int>();
        foreach (var c in _active.Candidates) tally[c] = 0;

        foreach (var (voter, cand) in _votes)
        {
            if (!eligibleVoters.Contains(voter)) continue;
            if (!tally.ContainsKey(cand)) continue;
            tally[cand]++;
        }

        // pick highest; tie => deterministic by string sort
        var winner = tally.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;

        _active = null;
        _votes.Clear();
        return (winner, tally);
    }
}
