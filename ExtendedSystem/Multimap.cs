using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedSystem
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Multimap")]
	public interface IMultimap<TKey, TValue> : IDictionary<TKey, TValue>
	{
		new ICollection<TValue> this[TKey key] { get; set; }
		bool Remove(TKey key, TValue value);
		bool Contains(TKey key, TValue value);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Multimap")]
	public sealed class Multimap<TKey, TValue> : IMultimap<TKey, TValue>
	{
		private Dictionary<TKey, List<TValue>> _dict;

		public Multimap() : this(0)
		{
		}

		public Multimap(int capacity) : this(capacity, EqualityComparer<TKey>.Default)
		{
		}

		public Multimap(IEqualityComparer<TKey> comparer) : this(0, comparer)
		{
		}

		public Multimap(int capacity, IEqualityComparer<TKey> comparer)
		{
			this._dict = new Dictionary<TKey, List<TValue>>(capacity, comparer);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Multimap(IEnumerable<KeyValuePair<TKey, TValue>> items) : this()
		{
			AddRange(items);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Multimap(IEnumerable<KeyValuePair<TKey, TValue>> items, IEqualityComparer<TKey> comparer) : this(comparer)
		{
			AddRange(items);
		}

		internal class ValuesInKey : ICollection<TValue>
		{
			internal Multimap<TKey, TValue> _instance;
			internal TKey _key;

			public int Count
			{
				get
				{
					if (this._instance._dict.TryGetValue(this._key, out var l))
						return l.Count;
					else
						return 0;
				}
			}

			public bool IsReadOnly => this._instance.IsReadOnly;

			public void Add(TValue item)
			{
				this._instance.Add(this._key, item);
			}

			public void Clear()
			{
				this._instance.Remove(this._key);
			}

			public bool Contains(TValue item)
			{
				return this._instance.Contains(this._key, item);
			}

			public void CopyTo(TValue[] array, int arrayIndex)
			{
				if (this._instance._dict.TryGetValue(this._key, out var l))
				{
					l.CopyTo(array, arrayIndex);
				}
			}

			public IEnumerator<TValue> GetEnumerator()
			{
				if (this._instance._dict.TryGetValue(this._key, out var l))
					return l.GetEnumerator();
				else
					return Enumerable.Empty<TValue>().GetEnumerator();
			}

			public bool Remove(TValue item)
			{
				return this._instance.Remove(this._key, item);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public ICollection<TValue> this[TKey key]
		{
			get
			{
				return new ValuesInKey() { _instance = this, _key = key };
			}
			set
			{
				if (value == null)
				{
					this._dict.Remove(key);
					return;
				}
				if (value is ValuesInKey vik && vik._instance == this && this._dict.Comparer.Equals(vik._key, key))
					return; // This is a no-op.
				var l = new List<TValue>(value);
				if (this._dict.TryGetValue(key, out var ol))
					ol.Clear();
				this._dict[key] = l;
			}
		}

		TValue IDictionary<TKey, TValue>.this[TKey key]
		{
			get
			{
				return this._dict[key].Single();
			}

			set
			{
				if (this._dict.TryGetValue(key, out var l))
				{
					l.Clear();
					l.Add(value);
				}
				else
					this._dict[key] = new List<TValue>() { value };
			}
		}

		public int Count => this._dict.Count;

		public bool IsReadOnly => false;

		public ICollection<TKey> Keys => this._dict.Keys;

		internal class SingleValueCollection : ICollection<TValue>
		{
			internal Multimap<TKey, TValue> _instance;

			public int Count => (from kvp in _instance._dict select kvp.Value.Count).Sum();

			public bool IsReadOnly => true;

			public void Add(TValue item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public void Clear()
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public bool Contains(TValue item)
			{
				return this._instance._dict.Any((kvp) => kvp.Value.Contains(item));
			}

			public void CopyTo(TValue[] array, int arrayIndex)
			{
				var ls = (from kvp in _instance._dict select kvp.Value).Aggregate<IEnumerable<TValue>>((l1, l2) => l1.Concat(l2)).ToArray();
				ls.CopyTo(array, arrayIndex);
			}

			public IEnumerator<TValue> GetEnumerator()
			{
				foreach (var kvp in this._instance._dict)
				{
					foreach (var v in kvp.Value)
						yield return v;
				}
			}

			public bool Remove(TValue item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		ICollection<TValue> IDictionary<TKey, TValue>.Values => new SingleValueCollection() { _instance = this };

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			if (items == null)
				throw new ArgumentNullException("items");
			foreach (var kvp in items)
				Add(kvp);
		}

		public void Add(TKey key, TValue value)
		{
			if (this._dict.TryGetValue(key, out var l))
				l.Add(value);
			else
				this._dict.Add(key, new List<TValue>() { value });
		}

		public void Clear()
		{
			this._dict.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return Contains(item.Key, item.Value);
		}

		public bool Contains(TKey key, TValue value)
		{
			if (this._dict.TryGetValue(key, out var l))
				return l.Contains(value);
			else
				return false;
		}

		public bool ContainsKey(TKey key)
		{
			return this._dict.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			var cnt = this._dict.Select((kvp) => kvp.Value.Select((v) => new KeyValuePair<TKey, TValue>(kvp.Key, v))).Aggregate((e1, e2) => e1.Concat(e2)).ToArray();
			cnt.CopyTo(array, arrayIndex);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			foreach (var kvp in this._dict)
			{
				foreach (var v in kvp.Value)
					yield return new KeyValuePair<TKey, TValue>(kvp.Key, v);
			}
		}

		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			return Remove(item.Key, item.Value);
		}

		public bool Remove(TKey key, TValue value)
		{
			if (this._dict.TryGetValue(key, out var l))
				return l.Remove(value);
			else
				return false;
		}

		public bool Remove(TKey key)
		{
			return this._dict.Remove(key);
		}

		bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
		{
			if (this._dict.TryGetValue(key, out var l))
			{
				if (l.Count == 1)
				{
					value = l[0];
					return true;
				}
			}
			value = default(TValue);
			return false;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
