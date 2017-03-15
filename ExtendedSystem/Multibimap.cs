using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ExtendedSystem
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Multibimap")]
	public interface IMultibimap<TLeft, TRight> : IBimap<TLeft, TRight>
	{
		new ICollection<TRight> this[TLeft key] { get; set; }
		new ICollection<TLeft> this[TRight key] { get; set; }
		bool Remove(TLeft left, TRight right);
		bool Contains(TLeft left, TRight right);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Multibimap")]
	public sealed class Multibimap<TLeft, TRight> : IMultibimap<TLeft, TRight>
	{
		// Use nullables so that we can use the entire hashcode.
		private struct Entry
		{
			internal KeyPair<TLeft, TRight>? _item; // null if this is a free entry
			internal KeyPair<int, int> _hash; // Invalid if !item.HasValue
							 // If this is a "free" entry, lnext == rnext
			internal int _lnext; // -1 if this is the last of the chain
			internal int _rnext; // -1 if this is the last of the chain
			internal int _lprev; // -1 if this is the first of the chain
			internal int _rprev; // -1 if this is the first of the chain
		}
		private Entry[] _items;
		private int[] _lefthash; // Empty buckets contain -1
		private int[] _righthash;
		private int _version = 0;
		private int _freeIndex; // -1 : no free entries - next addition will force a reallocation
		private int _freeCount;
		private IEqualityComparer<TLeft> _leftcmp;
		private IEqualityComparer<TRight> _rightcmp;

		// Performs a reallocation of the entry set.
		// A general condition is that items, lefthash, and righthash are all the same length.
		// desiredCapacity is clamped at a lower bound of either 16 or the current number of items in the dictionary - whichever is more
		// A reallocation is indicated if any of these conditions are met:
		// - Any of items, lefthash, or righthash is null, which is the case during construction.
		// - desiredCapacity is greater than the current array length
		// - desiredCapacity is less than half the current array length
		// A reallocation will resize the arrays to the smallest power of 2 greater than desiredCapacity, but not smaller than 16.
		// After reallocation, a rehash occurs.
		// If desiredCapacity is -1, 
		private void Realloc(int desiredCapacity)
		{
			Entry[] newItems;
			if (this._items != null && this._lefthash != null && this._righthash != null)
			{
				if (desiredCapacity < 16)
					desiredCapacity = 16;
				if (desiredCapacity < (this._items.Length - this._freeCount))
					desiredCapacity = this._items.Length - this._freeCount;
				if (desiredCapacity <= this._items.Length && desiredCapacity > this._items.Length / 2)
					return;
			}
			// Reallocation is happening.
			int targetSize = 16;
			while (targetSize < desiredCapacity)
				targetSize *= 2;
			newItems = new Entry[targetSize];
			int newFreeCt = targetSize;
			int newFreeIx = 0;
			if (this._items != null)
			{
				for (int i = 0; i < this._items.Length; ++i)
				{
					if (!this._items[i]._item.HasValue)
						continue;
					--newFreeCt;
					newItems[newFreeIx] = this._items[i];
					newItems[newFreeIx]._lprev = newItems[newFreeIx]._rprev = newItems[newFreeIx]._lnext = newItems[newFreeIx]._rnext = -1;
					newFreeIx++;
				}
			}
			this._items = newItems;
			this._freeCount = newFreeCt;
			this._freeIndex = newFreeIx;
			for (int i = newFreeIx; i < this._items.Length;)
			{
				int j = i++;
				this._items[j]._rnext = this._items[j]._lnext = (i < this._items.Length ? i : -1);
				this._items[j]._rprev = this._items[j]._lprev = (j == newFreeIx ? -1 : j - 1);
			}
			this._lefthash = null;
			this._righthash = null;
			Rehash(false);
		}

		public void Rehash(bool newHashes)
		{
			this._lefthash = new int[this._items.Length];
			this._righthash = new int[this._items.Length];
			this._lefthash.Fill(-1);
			this._righthash.Fill(-1);
			for (int i = 0; i < this._items.Length; ++i)
			{
				if (!this._items[i]._item.HasValue)
					continue;
				int lh = this._items[i]._hash.Left;
				int rh = this._items[i]._hash.Right;
				if (newHashes)
				{
					var p = this._items[i]._item.Value;
					lh = this._leftcmp.GetHashCode(p.Left);
					rh = this._rightcmp.GetHashCode(p.Right);
					this._items[i]._hash = new KeyPair<int, int>(lh, rh);
				}
				int lhix = this._lefthash.GetFromHash(lh);
				int rhix = this._righthash.GetFromHash(rh);
				this._items[i]._lprev = this._items[i]._rprev = -1;
				this._items[i]._lnext = lhix;
				if (lhix >= 0)
					this._items[lhix]._lprev = i;
				this._lefthash.SetFromHash(lh, i);
				this._items[i]._rnext = rhix;
				if (rhix >= 0)
					this._items[rhix]._rprev = i;
				this._righthash.SetFromHash(rh, i);
			}
		}

		private int FindLeft(TLeft key)
		{
			int hsh = this._leftcmp.GetHashCode(key);
			int ix = this._lefthash.GetFromHash(hsh);
			while (ix != -1)
			{
				Debug.Assert(this._items[ix]._item.HasValue);
				if (this._leftcmp.Equals(this._items[ix]._item.Value.Left, key))
					return ix;
				else
					ix = this._items[ix]._lnext;
			}
			return -1;
		}

		private int FindLeftNext(int ix)
		{
			int s;
			for (s = ix; ix != -1; ix = this._items[ix]._lnext)
			{
				Debug.Assert(this._items[ix]._item.HasValue);
				if (this._leftcmp.Equals(this._items[ix]._item.Value.Left, this._items[s]._item.Value.Left))
					return ix;
			}
			return -1;
		}

		private int FindRight(TRight key)
		{
			int hsh = this._rightcmp.GetHashCode(key);
			int ix = this._righthash.GetFromHash(hsh);
			while (ix != -1)
			{
				Debug.Assert(this._items[ix]._item.HasValue);
				if (this._rightcmp.Equals(this._items[ix]._item.Value.Right, key))
					return ix;
				else
					ix = this._items[ix]._rnext;
			}
			return -1;
		}

		private int FindRightNext(int ix)
		{
			int s;
			for (s = ix; ix != -1; ix = this._items[ix]._rnext)
			{
				Debug.Assert(this._items[ix]._item.HasValue);
				if (this._rightcmp.Equals(this._items[ix]._item.Value.Right, this._items[s]._item.Value.Right))
					return ix;
			}
			return -1;
		}

		public Multibimap() : this(0)
		{
		}

		public Multibimap(int capacity) : this(capacity, EqualityComparer<TLeft>.Default, EqualityComparer<TRight>.Default)
		{
		}

		public Multibimap(IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer) : this(0, leftComparer, rightComparer)
		{
		}

		public Multibimap(int capacity, IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer)
		{
			this._leftcmp = leftComparer;
			this._rightcmp = rightComparer;
			Realloc(capacity);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Multibimap(ICollection<KeyPair<TLeft, TRight>> source) : this(source, EqualityComparer<TLeft>.Default, EqualityComparer<TRight>.Default)
		{
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Multibimap(ICollection<KeyPair<TLeft, TRight>> source, IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer) : this(0, leftComparer, rightComparer)
		{
			AddRange(source);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Multibimap(ICollection<KeyValuePair<TLeft, TRight>> source) : this(source, EqualityComparer<TLeft>.Default, EqualityComparer<TRight>.Default)
		{
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Multibimap(ICollection<KeyValuePair<TLeft, TRight>> source, IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer) : this(0, leftComparer, rightComparer)
		{
			AddRange(source);
		}

		private void Unlink(int ix)
		{
			if (this._items[ix]._lprev < 0)
			{
				int lh = this._items[ix]._hash.Left;
				Debug.Assert(this._lefthash.GetFromHash(lh) == ix);
				this._lefthash.SetFromHash(lh, this._items[ix]._lnext);
			}
			else
			{
				this._items[this._items[ix]._lprev]._lnext = this._items[ix]._lnext;
			}
			if (this._items[ix]._lnext >= 0)
				this._items[this._items[ix]._lnext]._lprev = this._items[ix]._lprev;
			if (this._items[ix]._rprev < 0)
			{
				int rh = this._items[ix]._hash.Right;
				Debug.Assert(this._righthash.GetFromHash(rh) == ix);
				this._righthash.SetFromHash(rh, this._items[ix]._rnext);
			}
			else
			{
				this._items[this._items[ix]._rprev]._rnext = this._items[ix]._rnext;
			}
			if (this._items[ix]._rnext >= 0)
				this._items[this._items[ix]._rnext]._rprev = this._items[ix]._rprev;
			this._items[ix]._lnext = this._freeIndex;
			this._items[ix]._rnext = this._freeIndex;
			this._items[ix]._lprev = this._items[ix]._rprev = -1;
			this._items[ix]._item = null;
			this._items[this._freeIndex]._lprev = ix;
			this._items[this._freeIndex]._rprev = ix;
			this._freeIndex = ix;
			++this._freeCount;
		}

		private void Insert(TLeft left, TRight right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			if (this._freeCount < 1)
				Realloc(this._items.Length + 1);
			int lh = this._leftcmp.GetHashCode(left);
			int rh = this._rightcmp.GetHashCode(right);
			int lhix = this._lefthash.GetFromHash(lh);
			int rhix = this._righthash.GetFromHash(rh);
			Debug.Assert(lhix < 0 || this._items[lhix]._lprev == -1);
			Debug.Assert(rhix < 0 || this._items[rhix]._rprev == -1);
			int ix = this._freeIndex;
			this._freeIndex = this._items[ix]._lnext;
			this._items[ix]._lprev = this._items[ix]._rprev = -1;
			if (this._freeIndex >= 0)
				this._items[this._freeIndex]._lprev = this._items[this._freeIndex]._rprev = -1;
			--this._freeCount;
			this._items[ix]._lnext = lhix;
			if (lhix >= 0)
				this._items[lhix]._lprev = ix;
			this._items[ix]._rnext = rhix;
			if (rhix >= 0)
				this._items[rhix]._rprev = ix;
			this._lefthash.SetFromHash(lh, ix);
			this._righthash.SetFromHash(rh, ix);
			this._items[ix]._hash = new KeyPair<int, int>(lh, rh);
			this._items[ix]._item = new KeyPair<TLeft, TRight>(left, right);
			++this._version;
		}

		TLeft IBimap<TLeft, TRight>.this[TRight key]
		{
			get
			{
				int ix = FindRight(key);
				if (ix < 0)
					throw new KeyNotFoundException();
				if (FindRightNext(ix) >= 0)
					throw new InvalidOperationException("The key is not unique in this collection.");
				return this._items[ix]._item.Value.Left;
			}

			set
			{
				int ix;
				while ((ix = FindRight(key)) >= 0)
					Unlink(ix);
				Insert(value, key);
			}
		}

		TRight IBimap<TLeft, TRight>.this[TLeft key]
		{
			get
			{
				int ix = FindLeft(key);
				if (ix < 0)
					throw new KeyNotFoundException();
				if (FindLeftNext(ix) >= 0)
					throw new InvalidOperationException("The key is not unique in this collection.");
				return this._items[ix]._item.Value.Right;
			}

			set
			{
				int ix;
				while ((ix = FindLeft(key)) >= 0)
					Unlink(ix);
				Insert(key, value);
			}
		}

		public int Count
		{
			get
			{
				return this._items.Length - this._freeCount;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		internal class LeftCollection : ICollection<TLeft>
		{
			internal Multibimap<TLeft, TRight> _instance;
			internal bool _useKey;
			internal TRight _key;

			public int Count
			{
				get
				{
					if (this._useKey)
						return this._instance.Count;
					else
					{

						int ct = 0;
						for (int ix = this._instance.FindRight(this._key); ix >= 0; ix = this._instance.FindRightNext(ix))
						{
							Debug.Assert(this._instance._items[ix]._item.HasValue);
							++ct;
						}
						return ct;
					}
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return !this._useKey || this._instance.IsReadOnly;
				}
			}

			public void Add(TLeft item)
			{
				if (this.IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(this._useKey);
				this._instance.Add(item, this._key);
			}

			public void Clear()
			{
				if (this.IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(this._useKey);
				this._instance.Remove(this._key);
			}

			public bool Contains(TLeft item)
			{
				if (this._useKey)
					return this._instance.Contains(item, this._key);
				else
					return this._instance.ContainsKey(item);
			}

			public void CopyTo(TLeft[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < this.Count)
					throw new ArgumentException("Not enough space in array.");
				if (this._useKey)
				{
					for (int ix = this._instance.FindRight(this._key); ix >= 0; ix = this._instance.FindRightNext(ix))
						array[arrayIndex++] = this._instance._items[ix]._item.Value.Left;
				}
				else
				{
					for (int i = 0; i < this._instance._items.Length; ++i)
					{
						if (!this._instance._items[i]._item.HasValue)
							continue;
						array[arrayIndex++] = this._instance._items[i]._item.Value.Left;
					}
				}
			}

			public IEnumerator<TLeft> GetEnumerator()
			{
				if (this._useKey)
				{
					for (int ix = this._instance.FindRight(this._key); ix >= 0; ix = this._instance.FindRightNext(ix))
						yield return this._instance._items[ix]._item.Value.Left;
				}
				else
				{
					foreach (var kp in this._instance)
						yield return kp.Left;
				}
			}

			public bool Remove(TLeft item)
			{
				if (this.IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(this._useKey);
				return this._instance.Remove(item, this._key);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public ICollection<TLeft> LeftValues
		{
			get
			{
				return new LeftCollection { _instance = this, _useKey = false };
			}
		}

		internal class RightCollection : ICollection<TRight>
		{
			internal Multibimap<TLeft, TRight> _instance;
			internal bool _useKey;
			internal TLeft _key;


			public int Count
			{
				get
				{
					if (this._useKey)
						return this._instance.Count;
					else
					{

						int ct = 0;
						for (int ix = this._instance.FindLeft(this._key); ix >= 0; ix = this._instance.FindLeftNext(ix))
						{
							Debug.Assert(this._instance._items[ix]._item.HasValue);
							++ct;
						}
						return ct;
					}
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return !this._useKey || this._instance.IsReadOnly;
				}
			}

			public void Add(TRight item)
			{
				if (this.IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(this._useKey);
				this._instance.Add(this._key, item);
			}

			public void Clear()
			{
				if (this.IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(this._useKey);
				this._instance.Remove(this._key);
			}

			public bool Contains(TRight item)
			{
				if (this._useKey)
					return this._instance.Contains(this._key, item);
				else
					return this._instance.ContainsKey(item);
			}

			public void CopyTo(TRight[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < this.Count)
					throw new ArgumentException("Not enough space in array.");
				if (this._useKey)
				{
					for (int ix = this._instance.FindLeft(this._key); ix >= 0; ix = this._instance.FindLeftNext(ix))
						array[arrayIndex++] = this._instance._items[ix]._item.Value.Right;
				}
				else
				{
					for (int i = 0; i < this._instance._items.Length; ++i)
					{
						if (!this._instance._items[i]._item.HasValue)
							continue;
						array[arrayIndex++] = this._instance._items[i]._item.Value.Right;
					}
				}
			}

			public IEnumerator<TRight> GetEnumerator()
			{
				if (this._useKey)
				{
					for (int ix = this._instance.FindLeft(this._key); ix >= 0; ix = this._instance.FindLeftNext(ix))
						yield return this._instance._items[ix]._item.Value.Right;
				}
				else
				{
					foreach (var kp in this._instance)
						yield return kp.Right;
				}
			}

			public bool Remove(TRight item)
			{
				if (this.IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(this._useKey);
				return this._instance.Remove(this._key, item);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public ICollection<TRight> RightValues
		{
			get
			{
				return new RightCollection { _instance = this, _useKey = false };
			}
		}

		public ICollection<TLeft> this[TRight key]
		{
			get
			{
				return new LeftCollection { _instance = this, _useKey = true, _key = key };
			}

			set
			{
				if (value == null)
				{
					Remove(key);
					return;
				}
				var l = value.NoneOrThrow((o) => o == null, new Lazy<InvalidOperationException>(() => new InvalidOperationException("The collection may not contain null items."))).ToArray();
				Remove(key);
				foreach (var v in l)
					Add(v, key);
			}
		}

		public ICollection<TRight> this[TLeft key]
		{
			get
			{
				return new RightCollection { _instance = this, _useKey = true, _key = key };
			}

			set
			{
				if (value == null)
				{
					Remove(key);
					return;
				}
				var l = value.NoneOrThrow((o) => o == null, new Lazy<InvalidOperationException>(() => new InvalidOperationException("The collection may not contain null items."))).ToArray();
				Remove(key);
				foreach (var v in l)
					Add(key, v);
			}
		}

		public void Add(KeyPair<TLeft, TRight> item)
		{
			Add(item.Left, item.Right);
		}

		public void Add(TLeft left, TRight right)
		{
			Insert(left, right);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public void AddRange(ICollection<KeyPair<TLeft, TRight>> source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			int needed = source.Count;
			if (this._freeCount < needed)
				Realloc((this._items.Length - this._freeCount) + needed);
			foreach (var kp in source)
				Add(kp.Left, kp.Right);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public void AddRange(IEnumerable<KeyPair<TLeft, TRight>> source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			foreach (var kp in source)
				Add(kp.Left, kp.Right);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public void AddRange(ICollection<KeyValuePair<TLeft, TRight>> source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			int needed = source.Count;
			if (this._freeCount < needed)
				Realloc((this._items.Length - this._freeCount) + needed);
			foreach (var kvp in source)
				Add(kvp.Key, kvp.Value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public void AddRange(IEnumerable<KeyValuePair<TLeft, TRight>> source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			foreach (var kvp in source)
				Add(kvp.Key, kvp.Value);
		}

		public void Clear()
		{
			for (int i = 0; i < this._items.Length;)
			{
				int j = i++;
				this._items[j]._rnext = this._items[j]._lnext = (i < this._items.Length ? i : -1);
				this._items[j]._hash = new KeyPair<int, int>(0, 0);
				this._items[j]._item = null;
			}
			this._freeIndex = 0;
			this._freeCount = this._items.Length;
			this._lefthash.Fill(-1);
			this._righthash.Fill(-1);
			++this._version;
		}

		public bool Contains(KeyPair<TLeft, TRight> item)
		{
			for (int ix = FindLeft(item.Left); ix >= 0; ix = FindLeftNext(ix))
			{
				if (this._rightcmp.Equals(item.Right, this._items[ix]._item.Value.Right))
					return true;
			}
			return false;
		}

		public bool ContainsKey(TRight key)
		{
			return FindRight(key) >= 0;
		}

		public bool ContainsKey(TLeft key)
		{
			return FindLeft(key) >= 0;
		}

		public void CopyTo(KeyPair<TLeft, TRight>[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException("array");
			if ((array.Length - arrayIndex) < this.Count)
				throw new ArgumentException("Not enough space in array.");
			for (int ix = 0; ix < this._items.Length; ++ix)
			{
				if (!this._items[ix]._item.HasValue)
					continue;
				array[arrayIndex++] = this._items[ix]._item.Value;
			}
		}

		public IEnumerator<KeyPair<TLeft, TRight>> GetEnumerator()
		{
			int _ver = this._version;
			for (int ix = 0; ix < this._items.Length; ++ix)
			{
				if (_ver != this._version)
					throw new InvalidOperationException("The dictionary has been modified, so the enumerator is now invalid.");
				if (!this._items[ix]._item.HasValue)
					continue;
				yield return this._items[ix]._item.Value;
			}
		}

		public bool Remove(KeyPair<TLeft, TRight> item)
		{
			return Remove(item.Left, item.Right);
		}

		public bool Remove(TRight key)
		{
			bool any = false;
			int ix;
			while ((ix = FindRight(key)) >= 0)
			{
				Unlink(ix);
				any = true;
			}
			if (any)
				++this._version;
			return false;
		}

		public bool Remove(TLeft key)
		{
			bool any = false;
			int ix;
			while ((ix = FindLeft(key)) >= 0)
			{
				Unlink(ix);
				any = true;
			}
			if (any)
				++this._version;
			return false;
		}

		bool IBimap<TLeft, TRight>.TryGetValue(TRight key, out TLeft value)
		{
			int ix = FindRight(key);
			if (ix < 0)
			{
				value = default(TLeft);
				return false;
			}
			Debug.Assert(this._items[ix]._item.HasValue);
			if (FindRightNext(ix) >= 0)
			{
				value = default(TLeft);
				return false;
			}
			value = this._items[ix]._item.Value.Left;
			return true;
		}

		bool IBimap<TLeft, TRight>.TryGetValue(TLeft key, out TRight value)
		{
			int ix = FindLeft(key);
			if (ix < 0)
			{
				value = default(TRight);
				return false;
			}
			Debug.Assert(this._items[ix]._item.HasValue);
			if (FindLeftNext(ix) >= 0)
			{
				value = default(TRight);
				return false;
			}
			value = this._items[ix]._item.Value.Right;
			return true;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool Remove(TLeft left, TRight right)
		{
			for (int ix = FindLeft(left); ix >= 0; ix = FindLeftNext(ix))
			{
				if (this._rightcmp.Equals(right, this._items[ix]._item.Value.Right))
				{
					Unlink(ix);
					++this._version;
					return true;
				}
			}
			return false;
		}

		public bool Contains(TLeft left, TRight right)
		{
			for (int ix = FindLeft(left); ix >= 0; ix = FindLeftNext(ix))
			{
				if (this._rightcmp.Equals(right, this._items[ix]._item.Value.Right))
				{
					return true;
				}
			}
			return false;
		}
	}
}
