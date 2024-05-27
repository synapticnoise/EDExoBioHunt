namespace EDExoBioHunt;

public class StringCountTracker : CountTracker<string>
{
    public StringCountTracker(bool caseSensitive = false) : base(caseSensitive ? null : StringComparer.CurrentCultureIgnoreCase)
    {
    }
}

public class CountTracker<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, CountHolder> _counts;

    public CountTracker(IEqualityComparer<TKey>? comparer = null)
    {
        _counts = comparer != null ? new Dictionary<TKey, CountHolder>(comparer) : new Dictionary<TKey, CountHolder>();
    }

    public IEnumerable<TKey> Keys => _counts.Keys;

    public int this[TKey key] => _counts.TryGetValue(key, out var holder) ? holder.Count : 0;

    public IEnumerable<KeyValuePair<TKey, int>> Counts => _counts.Select(t => new KeyValuePair<TKey, int>(t.Key, t.Value.Count));

    public void Add(TKey key)
    {
        if (!_counts.TryGetValue(key, out var holder))
        {
            _counts.Add(key, new CountHolder());
            return;
        }

        holder.Add();
    }

    private class CountHolder
    {
        private int _count;

        public CountHolder()
        {
            _count = 1;
        }

        public void Add() => _count++;
        public int Count => _count;
    }
}