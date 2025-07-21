using System.Collections.Immutable;


namespace Hubcon.Shared.Core.Cache
{
    public class ImmutableCache<TKey, TValue> where TKey : notnull
    {
        private volatile ImmutableDictionary<TKey, TValue> _dict = ImmutableDictionary<TKey, TValue>.Empty;

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (_dict.TryGetValue(key, out var existing))
                return existing;

            var newValue = valueFactory(key);
            ImmutableInterlocked.Update(ref _dict, (dict, kvp) =>
            {
                if (dict.ContainsKey(kvp.Key))
                    return dict; // otro hilo lo insertó primero
                return dict.Add(kvp.Key, kvp.Value);
            }, new KeyValuePair<TKey, TValue>(key, newValue));

            return _dict[key];
        }

        public bool TryGetValue(TKey key, out TValue? value) => _dict.TryGetValue(key, out value);
    }
}
