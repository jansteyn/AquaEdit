namespace AquaEdit.Core;

/// <summary>
/// Least Recently Used cache for FileWindow objects
/// </summary>
public class LRUCache<TKey, TValue> where TKey : notnull where TValue : IDisposable
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _order;

    private class CacheItem
    {
        public TKey Key { get; set; } = default!;
        public TValue Value { get; set; } = default!;
    }

    public LRUCache(int capacity)
    {
        _capacity = capacity;
        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        _order = new LinkedList<CacheItem>();
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            // Move to front (most recently used)
            _order.Remove(node);
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Add(TKey key, TValue value)
    {
        if (_cache.ContainsKey(key))
        {
            // Update existing
            var node = _cache[key];
            node.Value.Value.Dispose();
            node.Value.Value = value;
            _order.Remove(node);
            _order.AddFirst(node);
            return;
        }

        // Check capacity
        if (_cache.Count >= _capacity)
        {
            // Remove least recently used
            var lru = _order.Last!;
            _order.RemoveLast();
            _cache.Remove(lru.Value.Key);
            lru.Value.Value.Dispose();
        }

        // Add new item
        var cacheItem = new CacheItem { Key = key, Value = value };
        var newNode = _order.AddFirst(cacheItem);
        _cache[key] = newNode;
    }

    public void Remove(TKey key)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _cache.Remove(key);
            node.Value.Value.Dispose();
        }
    }

    public void Clear()
    {
        foreach (var node in _order)
        {
            node.Value.Dispose();
        }
        _cache.Clear();
        _order.Clear();
    }
}