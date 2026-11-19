using System.Collections.Concurrent;
using ProductApi.Models;

namespace ProductApi.Services;

public class ProductStore
{
    private readonly ConcurrentDictionary<int, Product> _items = new();
    private int _nextId;

    public ProductStore()
    {
        for (var i = 1; i <= 100; i++) Add(new CreateProductRequest($"Product {i}", 10 + i, 100));
    }

    // Cursor paging: ids are assigned sequentially, so walking ids after the cursor is O(limit)
    // (the in-memory equivalent of an indexed "WHERE Id > @after ORDER BY Id LIMIT @limit").
    public (List<Product> Items, int? NextCursor) Page(int after, int limit)
    {
        var items = new List<Product>(limit);
        var last = Volatile.Read(ref _nextId);
        var id = after;
        while (items.Count < limit && id < last)
        {
            id++;
            if (_items.TryGetValue(id, out var p)) items.Add(p);
        }
        // More data exists if any id beyond the last returned one is still present.
        int? next = null;
        for (var probe = id + 1; probe <= last; probe++)
        {
            if (_items.ContainsKey(probe)) { next = items.Count > 0 ? items[^1].Id : after; break; }
        }
        return (items, next);
    }

    public Product? Get(int id) => _items.TryGetValue(id, out var p) ? p : null;

    public Product Add(CreateProductRequest r)
    {
        var id = Interlocked.Increment(ref _nextId);
        var p = new Product(id, r.Name, r.Price, r.Stock);
        _items[id] = p;
        return p;
    }

    public bool Delete(int id) => _items.TryRemove(id, out _);
}
