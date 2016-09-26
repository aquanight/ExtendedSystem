using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedSystem
{
	/// <summary>
	/// Represents an inverted dictionary. An inverted dictionary is when a dictionary has its values mapped to keys instead of the reverse.
	/// Values aren't unique in a normal dictionary, so the keys are presented as an ICollection.
	/// 
	/// The terminology regarding "keys" and "values" may get confusing.
	/// This is because keys in the original dictionary are values in the inverted dictionary, and vice-versa.
	/// To keep them straight, the terms "keys" and "values" are used from the perspective of the originating dictionary.
	/// For example, the Keys property returns values and the Values property returns keys, and you index this dictionary with a value and receive a collection of keys.
	/// 
	/// This class is a thin wrapper around the originating dictionary. Any modifications through this class will modify the originating dictionary, and any modifications to the
	/// original dictionary are visible in the corresponding inverted instance.
	/// The inverted dictionary is readonly if and only if (and when and only when) the original is.
	/// 
	/// </summary>
	/// <typeparam name="K">The key type of the original dictionary.</typeparam>
	/// <typeparam name="V">The value type of the original dictionary.</typeparam>
	public sealed class InvertedDictionary<TKey, TValue> : IDictionary<TValue, ICollection<TKey>>
	{
		private IDictionary<TKey, TValue> source;

		public InvertedDictionary(IDictionary<TKey, TValue> source)
		{
			this.source = source;
		}

		internal class KeysWithValue : ICollection<TKey>
		{
			internal InvertedDictionary<TKey, TValue> outer;
			internal TValue val;
			internal bool nomod;

			internal bool _finder(KeyValuePair<TKey, TValue> kvp)
			{
				return kvp.Equals(val);
			}

			internal KeysWithValue(InvertedDictionary<TKey, TValue> otr, TValue v, bool noModify)
			{
				outer = otr;
				val = v;
				nomod = noModify;
			}

			public int Count
			{
				get
				{
					return outer.source.Count(_finder);
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return nomod || outer.IsReadOnly;
				}
			}

			public void Add(TKey item)
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				outer.source.Add(item, val);
			}

			public void Clear()
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				KeyValuePair<TKey, TValue>? kvp;
				while ((kvp = outer.source.FirstOrNull(_finder)).HasValue)
				{
					outer.source.Remove(kvp.Value.Key);
				}
			}

			public bool Contains(TKey item)
			{
				return outer.source.Any(_finder);
			}

			public void CopyTo(TKey[] array, int arrayIndex)
			{
				TKey[] keys = outer.source.Where(_finder).Select((kvp) => kvp.Key).ToArray();
				keys.CopyTo(array, arrayIndex);
			}

			public IEnumerator<TKey> GetEnumerator()
			{
				foreach (var kvp in outer.source.Where(_finder))
				{
					yield return kvp.Key;
				}
			}

			public bool Remove(TKey item)
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				TValue _v;
				if (!outer.source.TryGetValue(item, out _v))
					return false;
				if (_v.Equals(item))
					return outer.source.Remove(item);
				return false;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		/// <summary>
		/// Retrieves the collection of keys which have a given value assigned to them.
		/// Only keys which actually exist in the dictionary are retrieved.
		/// 
		/// Unlike other dictionaries, if no key in the original dictionary has an associated value, the returned collection is empty
		/// (and can be modified to add such keys, for example), rather than resulting in an exception.
		/// The collection returned can be modified, which will result in assigning or removing keys in the original dictionary:
		/// -- Count: returns the number of keys that have the value assigned
		/// -- CopyTo: copies to an array the keys that have the value assigned
		/// -- Contains: determines if the requested key has the value assigned
		/// -- GetEnumerator: enumerate the keys that have the value assigned
		/// -- Add: adds a key to the dictionary with this value - see the originating dictionary's Add method for if an exception might occur
		/// -- Remove: removes the key from the dictionary only if it has this value assigned. Returns false and does not remove if the key exists but is assigned a nonequal value
		/// -- Clear: removes all keys from the dictionary that have this value assigned
		/// -- IsReadOnly: true if and only if the this dictionary and thus the originating dictionary IsReadOnly
		/// 
		/// If you assign a collection to this property, all keys assigned this value are removed,
		/// and those in the assigned collection are assigned this value in the originating dictionary using that dictionary's Add method. If an exception occurs,
		/// no change is made.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public ICollection<TKey> this[TValue key]
		{
			get
			{
				return new KeysWithValue(this, key, false);
			}

			set
			{
				if (source.IsReadOnly)
					throw new NotSupportedException("This dictionary is read-only.");
				TKey[] keys = value.ToArray();
				var kwv = new KeysWithValue(this, key, false);
				TKey[] original = kwv.ToArray();
				kwv.Clear();
				try
				{
					foreach (TKey k in keys)
						kwv.Add(k);
				}
				catch (Exception)
				{
					kwv.Clear();
					foreach (TKey k in original)
						kwv.Add(k);
					throw;
				}
			}
		}

		/// <summary>
		/// Returns the number of distinct values in this dictionary.
		/// </summary>
		public int Count
		{
			get
			{
				return source.Select((kvp) => kvp.Value).Distinct().Count();
			}
		}

		/// <summary>
		/// True if this dictionary is readonly, which happens when the originating dictionary is readonly.
		/// </summary>
		public bool IsReadOnly
		{
			get
			{
				return source.IsReadOnly;
			}
		}

		internal class ValueCollection : ICollection<TValue>
		{
			InvertedDictionary<TKey, TValue> outer;

			internal ValueCollection(InvertedDictionary<TKey, TValue> otr)
			{
				outer = otr;
			}

			public int Count
			{
				get
				{
					return outer.Count;
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
				return outer.source.Any((kvp) => kvp.Value.Equals(item));
			}

			public void CopyTo(TValue[] array, int arrayIndex)
			{
				outer.source.GroupBy((kvp) => kvp.Value).Select((grp) => grp.Key).CopyTo(array, arrayIndex);
			}

			public IEnumerator<TValue> GetEnumerator()
			{
				return outer.source.GroupBy((kvp) => kvp.Value).Select((grp) => grp.Key).GetEnumerator();
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

		/// <summary>
		/// Returns a readonly collection describing the distinct values in this dictionary.
		/// </summary>
		public ICollection<TValue> Keys
		{
			get
			{
				return new ValueCollection(this);
			}
		}

		internal class KeyCollection : ICollection<ICollection<TKey>>
		{
			InvertedDictionary<TKey, TValue> outer;

			internal KeyCollection(InvertedDictionary<TKey, TValue> otr)
			{
				outer = otr;
			}

			public int Count
			{
				get
				{
					return outer.Count;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return true;
				}
			}

			public void Add(ICollection<TKey> item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public void Clear()
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public bool Contains(ICollection<TKey> item)
			{
				return outer.source.GroupBy((kvp) => kvp.Value).Any((grp) => item.All((i) => grp.Any((kvp) => kvp.Key.Equals(i))));
			}

			public void CopyTo(ICollection<TKey>[] array, int arrayIndex)
			{
				outer.source.GroupBy((kvp) => kvp.Value).Select((grp) => new KeysWithValue(outer, grp.Key, true)).CopyTo(array, arrayIndex);
			}

			public IEnumerator<ICollection<TKey>> GetEnumerator()
			{
				foreach (var grp in outer.source.GroupBy((kvp) => kvp.Value))
					yield return new KeysWithValue(outer, grp.Key, true);
			}

			public bool Remove(ICollection<TKey> item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		/// <summary>
		/// Returns a readonly collection of key collections. Each key collection is readonly and corresponds to a value.
		/// They are returned in the same order as the values in the Keys property.
		/// </summary>
		public ICollection<ICollection<TKey>> Values
		{
			get
			{
				return new KeyCollection(this);
			}
		}

		/// <summary>
		/// Adds a collection of keys assigned a value. All of the keys in the originating dictionary are assigned the specified value.
		/// See the originating dictionary's Add method for information regarding duplicate keys and exceptions.
		/// </summary>
		/// <param name="item"></param>
		public void Add(KeyValuePair<TValue, ICollection<TKey>> item)
		{
			Add(item.Key, item.Value);
		}

		/// <summary>
		/// Adds a collection of keys assigned a value. All of the keys in the originating dictionary are assigned the specified value.
		/// See the originating dictionary's Add method for information regarding duplicate keys and exceptions.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		public void Add(TValue key, ICollection<TKey> value)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			var kwv = this[key];
			foreach (TKey k in value)
				kwv.Add(k);
		}

		/// <summary>
		/// Removes all keys and values from the originating dictionary.
		/// </summary>
		public void Clear()
		{
			source.Clear();
		}

		/// <summary>
		/// Returns true if all of the keys in the key collection are assigned the indicated value in the originating dictionary.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(KeyValuePair<TValue, ICollection<TKey>> item)
		{
			var kwv = this[item.Key];
			return item.Value.All((k) => kwv.Contains(k));
		}

		/// <summary>
		/// Returns true if any key in the originating dictionary is assigned the given value.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool ContainsKey(TValue key)
		{
			return source.Any((kvp) => kvp.Value.Equals(key));
		}

		public void CopyTo(KeyValuePair<TValue, ICollection<TKey>>[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException("array");
			var cnt = source.GroupBy((kvp) => kvp.Value);
			if (cnt.Count() > (array.Length - arrayIndex))
				throw new ArgumentException("Not enough space in the array.");
			var e = cnt.GetEnumerator();
			while (e.MoveNext())
			{
				KeyValuePair<TValue, ICollection<TKey>> item = new KeyValuePair<TValue, ICollection<TKey>>(e.Current.Key, new KeysWithValue(this, e.Current.Key, true));
				array[arrayIndex++] = item;
			}
		}

		public IEnumerator<KeyValuePair<TValue, ICollection<TKey>>> GetEnumerator()
		{
			var cnt = source.GroupBy((kvp) => kvp.Value);
			var e = cnt.GetEnumerator();
			while (e.MoveNext())
			{
				KeyValuePair<TValue, ICollection<TKey>> item = new KeyValuePair<TValue, ICollection<TKey>>(e.Current.Key, new KeysWithValue(this, e.Current.Key, true));
				yield return item;
			}
		}

		/// <summary>
		/// Removes the given set of keys only if they are assigned the given value. Returns true if any such key was actually removed. See the originating dictionary's Remove
		/// method for details.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(KeyValuePair<TValue, ICollection<TKey>> item)
		{
			bool any = false;
			var kwv = this[item.Key];
			foreach (TKey k in item.Value)
				if (kwv.Remove(k))
					any = true;
			return any;
		}

		/// <summary>
		/// Removes all keys that are assigned the given value. Returns true if any such key was successfully removed. See the originating dictionary's Remove method for details.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool Remove(TValue key)
		{
			bool any = false;
			var kwv = this[key];
			any = kwv.Any();
			kwv.Clear();
			return any;
		}

		/// <summary>
		/// Provided for interface completion. This method is not needed because indexing the inverted dictionary never fails.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		bool IDictionary<TValue, ICollection<TKey>>.TryGetValue(TValue key, out ICollection<TKey> value)
		{
			value = this[key];
			return true;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		/// Takes a snapshot of the inverted dictionary, returning a new dictionary instance which is a copy of the state of the inverted dictionary at the time it is called.
		/// Thereafter the returned instance is independant, and modifications are not visible in it.
		/// </summary>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public IDictionary<TValue, ICollection<TKey>> Snapshot()
		{
			var dict = new Dictionary<TValue, ICollection<TKey>>();
			var e = this.GetEnumerator();
			while (e.MoveNext())
			{
				dict[e.Current.Key] = e.Current.Value.ToArray();
			}
			return dict;
		}
	}


	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
	public static class MoreDictionary
	{
		/// <summary>
		/// Turns a dictionary inside-out. A dictionary normally maps a set of unique keys each to a particular (not necessarily unique) value.
		/// An inverted dictionary is one where the values are mapped to the list of keys they are assigned to. Values aren't unique, so the keys are provided as an collection.
		/// Operations can be performed on this dictionary: they will be executed in terms of the source dictionary (e.g. adding a key to the list of keys mapped to a value
		/// adds the key/value pair to the source dictionary).
		/// </summary>
		/// <typeparam name="TKey"></typeparam>
		/// <typeparam name="TValue"></typeparam>
		/// <param name="dictionary"></param>
		/// <returns></returns>
		public static InvertedDictionary<TKey, TValue> Invert<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
		{
			return new InvertedDictionary<TKey, TValue>(dictionary);
		}

		// Implement the set of Linq methods on a dictionary in terms of the key or value individually (instead of the key/value pair).

		public static bool AnyKey<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Func<TKey, bool> keyPredicate)
		{
			return dictionary.Any((kvp) => keyPredicate(kvp.Key));
		}

		public static bool AnyValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Func<TValue, bool> valuePredicate)
		{
			return dictionary.Any((kvp) => valuePredicate(kvp.Value));
		}

		public static bool AllKeys<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Func<TKey, bool> keyPredicate)
		{
			return dictionary.All((kvp) => keyPredicate(kvp.Key));
		}

		public static bool AllValues<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Func<TValue, bool> keyPredicate)
		{
			return dictionary.All((kvp) => keyPredicate(kvp.Value));
		}

		internal class WhereKeyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
		{
			internal IDictionary<TKey, TValue> source;
			internal Func<TKey, bool> predicate;

			private bool kvpsel(KeyValuePair<TKey, TValue> kvp)
			{
				return predicate(kvp.Key);
			}

			public ICollection<TKey> Keys
			{
				get
				{
					return this.source.Keys.Where(predicate).AsCollection();
				}
			}

			public ICollection<TValue> Values
			{
				get
				{
					return this.source.Keys.Where(predicate).Select((k) => source[k]).AsCollection();
				}
			}

			public int Count
			{
				get
				{
					return this.source.Count(kvpsel);
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return this.source.IsReadOnly;
				}
			}

			public TValue this[TKey key]
			{
				get
				{
					if (predicate(key))
						return this.source[key];
					else
						throw new KeyNotFoundException();
				}

				set
				{
					if (predicate(key))
						this.source[key] = value;
					else
						throw new ArgumentException("The key is not accepted by this filtered dictionary.");
				}
			}

			public bool ContainsKey(TKey key)
			{
				return predicate(key) && this.source.ContainsKey(key);
			}

			public void Add(TKey key, TValue value)
			{
				if (!predicate(key))
					throw new ArgumentException("The key is not accepted by this filtered dictionary.");
				this.source.Add(key, value);
			}

			public bool Remove(TKey key)
			{
				return predicate(key) && this.source.Remove(key);
			}

			public bool TryGetValue(TKey key, out TValue value)
			{
				if (!predicate(key))
				{
					value = default(TValue);
					return false;
				}
				return this.source.TryGetValue(key, out value);
			}

			public void Add(KeyValuePair<TKey, TValue> item)
			{
				if (!predicate(item.Key))
					throw new ArgumentException("The key is not accepted by this filtered dictionary.");
				this.source.Add(item);
			}

			public void Clear()
			{
				TKey[] keys = this.Keys.ToArray();
				foreach (TKey k in keys)
					this.source.Remove(k);
			}

			public bool Contains(KeyValuePair<TKey, TValue> item)
			{
				return predicate(item.Key) && this.source.Contains(item);
			}

			public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
			{
				KeyValuePair<TKey, TValue>[] items = this.source.Where(kvpsel).ToArray();
				items.CopyTo(array, arrayIndex);
			}

			public bool Remove(KeyValuePair<TKey, TValue> item)
			{
				return predicate(item.Key) && this.source.Remove(item);
			}

			public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
			{
				return this.source.Where(kvpsel).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}

		internal class WhereValueDictionary<TKey, TValue> : IDictionary<TKey, TValue>
		{
			internal IDictionary<TKey, TValue> source;
			internal Func<TValue, bool> predicate;

			private bool kvpsel(KeyValuePair<TKey, TValue> kvp)
			{
				return predicate(kvp.Value);
			}

			public ICollection<TKey> Keys
			{
				get
				{
					return this.source.Where(kvpsel).Select((kvp) => kvp.Key).AsCollection();
				}
			}

			public ICollection<TValue> Values
			{
				get
				{
					return this.source.Where(kvpsel).Select((kvp) => kvp.Value).AsCollection();
				}
			}

			public int Count
			{
				get
				{
					return this.source.Count(kvpsel);
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return this.source.IsReadOnly;
				}
			}

			public TValue this[TKey key]
			{
				get
				{
					TValue v = this.source[key];
					if (!predicate(v))
						throw new KeyNotFoundException();
					return v;
				}

				set
				{
					if (!predicate(value))
						throw new InvalidOperationException("Value is not accepted by this filtered dictionary.");
					this.source[key] = value;
				}
			}

			public bool ContainsKey(TKey key)
			{
				return this.source.Any((kvp) => kvp.Key.Equals(key) && predicate(kvp.Value));
			}

			public void Add(TKey key, TValue value)
			{
				if (!predicate(value))
					throw new InvalidOperationException("Value is not accepted by this filtered dictionary.");
				this.source.Add(key, value);
			}

			public bool Remove(TKey key)
			{
				return this.source.ContainsKey(key) && predicate(this.source[key]) && this.source.Remove(key);
			}

			public bool TryGetValue(TKey key, out TValue value)
			{
				bool b = this.source.TryGetValue(key, out value);
				if (b && predicate(value))
					return true;
				value = default(TValue);
				return false;
			}

			public void Add(KeyValuePair<TKey, TValue> item)
			{
				if (!predicate(item.Value))
					throw new InvalidOperationException("Value is not accepted by this filtered dictionary.");
				this.source.Add(item);
			}

			public void Clear()
			{
				TKey[] keys = this.Keys.ToArray();
				foreach (TKey k in keys)
					this.source.Remove(k);
			}

			public bool Contains(KeyValuePair<TKey, TValue> item)
			{
				return predicate(item.Value) && this.source.Contains(item);
			}

			public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
			{
				KeyValuePair<TKey, TValue>[] items = this.source.Where(kvpsel).ToArray();
				items.CopyTo(array, arrayIndex);
			}

			public bool Remove(KeyValuePair<TKey, TValue> item)
			{
				return predicate(item.Value) && this.source.Remove(item);
			}

			public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
			{
				return this.source.Where(kvpsel).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}

		internal class SelectValueDictionary<TKey, TSource, TResult> : IDictionary<TKey, TResult>
		{
			internal IDictionary<TKey, TSource> source;
			internal Func<TSource, TResult> selector;

			public ICollection<TKey> Keys
			{
				get
				{
					return this.source.Keys;
				}
			}

			public ICollection<TResult> Values
			{
				get
				{
					return this.source.Values.Select(selector).AsCollection();
				}
			}

			public int Count
			{
				get
				{
					return this.source.Count;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return true;
				}
			}

			public TResult this[TKey key]
			{
				get
				{
					return selector(this.source[key]);
				}

				set
				{
					throw new NotSupportedException("This dictionary is read-only.");
				}
			}

			public bool ContainsKey(TKey key)
			{
				return this.source.ContainsKey(key);
			}

			public void Add(TKey key, TResult value)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public bool Remove(TKey key)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public bool TryGetValue(TKey key, out TResult value)
			{
				TSource v;
				if (this.source.TryGetValue(key, out v))
				{
					value = selector(v);
					return true;
				}
				else
				{
					value = default(TResult);
					return false;
				}
			}

			public void Add(KeyValuePair<TKey, TResult> item)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public void Clear()
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public bool Contains(KeyValuePair<TKey, TResult> item)
			{
				return this.source.ContainsKey(item.Key) && Object.Equals(item.Value, selector(this.source[item.Key]));
			}

			public void CopyTo(KeyValuePair<TKey, TResult>[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				KeyValuePair<TKey, TSource>[] temp = new KeyValuePair<TKey, TSource>[array.Length];
				this.source.CopyTo(temp, arrayIndex);
				for (int i = arrayIndex; i < array.Length; ++i)
					array[i] = new KeyValuePair<TKey, TResult>(temp[i].Key, selector(temp[i].Value));
			}

			public bool Remove(KeyValuePair<TKey, TResult> item)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public IEnumerator<KeyValuePair<TKey, TResult>> GetEnumerator()
			{
				return this.source.Select((kvp) => new KeyValuePair<TKey, TResult>(kvp.Key, selector(kvp.Value))).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}

		public static IDictionary<TKey, TResult> CastValues<TKey, TSource, TResult>(this IDictionary<TKey, TSource> dictionary)
		{
			return new SelectValueDictionary<TKey, TSource, TResult>() { source = dictionary, selector = (v) => (TResult)((object)v) };
		}

		public static bool ContainsValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TValue value)
		{
			return dictionary.AnyValue((v) => v.Equals(value));
		}

		public static IDictionary<TKey, TValue> ExceptKeys<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<TKey> keys)
		{
			return new WhereKeyDictionary<TKey, TValue>() { source = dictionary, predicate = (k) => !keys.Contains(k) };
		}

		internal class JoinDictionary<TKey, TOuter, TInner, TResult> : IDictionary<TKey, TResult>
		{
			internal IDictionary<TKey, TOuter> outer;
			internal IDictionary<TKey, TInner> inner;
			internal Func<TOuter, TInner, TResult> selector;

			public ICollection<TKey> Keys
			{
				get
				{
					return this.outer.Keys.Intersect(this.inner.Keys).AsCollection();
				}
			}

			public ICollection<TResult> Values
			{
				get
				{
					return this.Keys.Select((k) => selector(this.outer[k], this.inner[k])).AsCollection();
				}
			}

			public int Count
			{
				get
				{
					return this.Keys.Count;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return true;
				}
			}

			public TResult this[TKey key]
			{
				get
				{
					return selector(this.outer[key], this.inner[key]);
				}

				set
				{
					throw new NotSupportedException("This dictionary is read-only.");
				}
			}

			public bool ContainsKey(TKey key)
			{
				return this.outer.ContainsKey(key) && this.inner.ContainsKey(key);
			}

			public void Add(TKey key, TResult value)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public bool Remove(TKey key)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public bool TryGetValue(TKey key, out TResult value)
			{
				TOuter v1;
				TInner v2;
				if (this.outer.TryGetValue(key, out v1) && this.inner.TryGetValue(key, out v2))
				{
					value = selector(v1, v2);
					return true;
				}
				else
				{
					value = default(TResult);
					return false;
				}
			}

			public void Add(KeyValuePair<TKey, TResult> item)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public void Clear()
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public bool Contains(KeyValuePair<TKey, TResult> item)
			{
				return this.outer.ContainsKey(item.Key) && this.inner.ContainsKey(item.Key) && Object.Equals(item.Value, selector(this.outer[item.Key], this.inner[item.Key]));
			}

			public void CopyTo(KeyValuePair<TKey, TResult>[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				var temp = this.ToArray();
				temp.CopyTo(array, arrayIndex);
			}

			public bool Remove(KeyValuePair<TKey, TResult> item)
			{
				throw new NotSupportedException("This dictionary is read-only.");
			}

			public IEnumerator<KeyValuePair<TKey, TResult>> GetEnumerator()
			{
				return this.Keys.Select((k) => new KeyValuePair<TKey, TResult>(k, this[k])).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}

		public static IDictionary<TKey, TResult> Join<TKey, TValue1, TValue2, TResult>(this IDictionary<TKey, TValue1> outer, IDictionary<TKey, TValue2> inner, Func<TValue1, TValue2, TResult> resultSelector)
		{
			return new JoinDictionary<TKey, TValue1, TValue2, TResult>() { outer = outer, inner = inner, selector = resultSelector };
		}

		public static IDictionary<TKey, TResult> OfTypeValues<TKey, TSource, TResult>(this IDictionary<TKey, TSource> dictionary)
		{
			return dictionary.WhereValue((v) => v is TResult).SelectValues((v) => (TResult)((object)v));
		}

		public static IDictionary<TKey, TResult> SelectValues<TKey, TSource, TResult>(this IDictionary<TKey, TSource> dictionary, Func<TSource, TResult> selector)
		{
			return new SelectValueDictionary<TKey, TSource, TResult>() { source = dictionary, selector = selector };
		}

		public static IDictionary<TKey, TValue> WhereKeys<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Func<TKey, bool> predicate)
		{
			return new WhereKeyDictionary<TKey, TValue>() { source = dictionary, predicate = predicate };
		}

		public static IDictionary<TKey, TValue> WhereValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Func<TValue, bool> predicate)
		{
			return new WhereValueDictionary<TKey, TValue>() { source = dictionary, predicate = predicate };
		}
	}
}
