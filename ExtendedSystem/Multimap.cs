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
		private Dictionary<TKey, List<TValue>> dict;

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
			dict = new Dictionary<TKey, List<TValue>>(capacity, comparer);
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
			internal Multimap<TKey, TValue> instance;
			internal TKey key;

			public int Count
			{
				get
				{
					List<TValue> l;
					if (instance.dict.TryGetValue(key, out l))
						return l.Count;
					else
						return 0;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return instance.IsReadOnly;
				}
			}

			public void Add(TValue item)
			{
				instance.Add(key, item);
			}

			public void Clear()
			{
				instance.Remove(key);
			}

			public bool Contains(TValue item)
			{
				return instance.Contains(key, item);
			}

			public void CopyTo(TValue[] array, int arrayIndex)
			{
				List<TValue> l;
				if (instance.dict.TryGetValue(key, out l))
				{
					l.CopyTo(array, arrayIndex);
				}
			}

			public IEnumerator<TValue> GetEnumerator()
			{
				List<TValue> l;
				if (instance.dict.TryGetValue(key, out l))
					return l.GetEnumerator();
				else
					return Enumerable.Empty<TValue>().GetEnumerator();
			}

			public bool Remove(TValue item)
			{
				return instance.Remove(key, item);
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
				return new ValuesInKey() { instance = this, key = key };
			}
			set
			{
				if (value == null)
				{
					dict.Remove(key);
					return;
				}
				ValuesInKey vik = value as ValuesInKey;
				if (vik != null && vik.instance == this && dict.Comparer.Equals(vik.key, key))
					return; // This is a no-op.
				List<TValue> l = new List<TValue>(value);
				List<TValue> ol;
				if (dict.TryGetValue(key, out ol))
					ol.Clear();
				dict[key] = l;
			}
		}

		TValue IDictionary<TKey, TValue>.this[TKey key]
		{
			get
			{
				return dict[key].Single();
			}

			set
			{
				List<TValue> l;
				if (dict.TryGetValue(key, out l))
				{
					l.Clear();
					l.Add(value);
				}
				else
					dict[key] = new List<TValue>() { value };
			}
		}

		public int Count
		{
			get
			{
				return dict.Count;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		public ICollection<TKey> Keys
		{
			get
			{
				return dict.Keys;
			}
		}

		internal class SingleValueCollection : ICollection<TValue>
		{
			internal Multimap<TKey, TValue> instance;

			public int Count
			{
				get
				{
					return (from kvp in instance.dict select kvp.Value.Count).Sum();
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return true;
				}
			}

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
				return instance.dict.Any((kvp) => kvp.Value.Contains(item));
			}

			public void CopyTo(TValue[] array, int arrayIndex)
			{
				var ls = (from kvp in instance.dict select kvp.Value).Aggregate<IEnumerable<TValue>>((l1, l2) => l1.Concat(l2)).ToArray();
				ls.CopyTo(array, arrayIndex);
			}

			public IEnumerator<TValue> GetEnumerator()
			{
				foreach (var kvp in instance.dict)
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

		ICollection<TValue> IDictionary<TKey, TValue>.Values
		{
			get
			{
				return new SingleValueCollection() { instance = this };
			}
		}

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
			List<TValue> l;
			if (dict.TryGetValue(key, out l))
				l.Add(value);
			else
				dict.Add(key, new List<TValue>() { value });
		}

		public void Clear()
		{
			dict.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return Contains(item.Key, item.Value);
		}

		public bool Contains(TKey key, TValue value)
		{
			List<TValue> l;
			if (dict.TryGetValue(key, out l))
				return l.Contains(value);
			else
				return false;
		}

		public bool ContainsKey(TKey key)
		{
			return dict.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			var cnt = dict.Select((kvp) => kvp.Value.Select((v) => new KeyValuePair<TKey, TValue>(kvp.Key, v))).Aggregate((e1, e2) => e1.Concat(e2)).ToArray();
			cnt.CopyTo(array, arrayIndex);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			foreach (var kvp in dict)
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
			List<TValue> l;
			if (dict.TryGetValue(key, out l))
				return l.Remove(value);
			else
				return false;
		}

		public bool Remove(TKey key)
		{
			return dict.Remove(key);
		}

		bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
		{
			List<TValue> l;
			if (dict.TryGetValue(key, out l))
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
