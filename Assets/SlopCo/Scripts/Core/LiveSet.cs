using System.Collections.Generic;

namespace SlopCo.Core
{
    /// <summary>
    /// The live instances of one component type, kept current by the components themselves (Add on spawn, Remove
    /// on despawn), so per-frame readers walk a short list instead of scanning the scene. FindObjectsByType
    /// allocates a fresh array and visits every object in every loaded scene; the HUD fuse meter did that every
    /// frame on every client and each rat did it every frame on the host. Insertion order; Add is idempotent and
    /// ignores null. Main thread only. Iterate with an index (Count / this[i]) so reading never allocates, and
    /// null-check each entry: a component destroyed without a despawn compares equal to null until it is removed.
    /// </summary>
    public sealed class LiveSet<T> where T : class
    {
        private readonly List<T> _items = new List<T>();

        public int Count => _items.Count;
        public T this[int index] => _items[index];

        public void Add(T item)
        {
            if (item != null && !_items.Contains(item)) _items.Add(item);
        }

        public bool Remove(T item) => item != null && _items.Remove(item);

        public void Clear() => _items.Clear();
    }
}
