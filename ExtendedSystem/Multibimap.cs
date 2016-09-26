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
			internal KeyPair<TLeft, TRight>? item; // null if this is a free entry
			internal KeyPair<int, int> hash; // Invalid if !item.HasValue
							 // If this is a "free" entry, lnext == rnext
			internal int lnext; // -1 if this is the last of the chain
			internal int rnext; // -1 if this is the last of the chain
			internal int lprev; // -1 if this is the first of the chain
			internal int rprev; // -1 if this is the first of the chain
		}
		private Entry[] items;
		private int[] lefthash; // Empty buckets contain -1
		private int[] righthash;
		private int version = 0;
		private int freeIndex; // -1 : no free entries - next addition will force a reallocation
		private int freeCount;
		private IEqualityComparer<TLeft> leftcmp;
		private IEqualityComparer<TRight> rightcmp;

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
			if (items != null && lefthash != null && righthash != null)
			{
				if (desiredCapacity < 16)
					desiredCapacity = 16;
				if (desiredCapacity < (items.Length - freeCount))
					desiredCapacity = items.Length - freeCount;
				if (desiredCapacity <= items.Length && desiredCapacity > items.Length / 2)
					return;
			}
			// Reallocation is happening.
			int targetSize = 16;
			while (targetSize < desiredCapacity)
				targetSize *= 2;
			newItems = new Entry[targetSize];
			int newFreeCt = targetSize;
			int newFreeIx = 0;
			if (items != null)
			{
				for (int i = 0; i < items.Length; ++i)
				{
					if (!items[i].item.HasValue)
						continue;
					--newFreeCt;
					newItems[newFreeIx] = items[i];
					newItems[newFreeIx].lprev = newItems[newFreeIx].rprev = newItems[newFreeIx].lnext = newItems[newFreeIx].rnext = -1;
					newFreeIx++;
				}
			}
			items = newItems;
			freeCount = newFreeCt;
			freeIndex = newFreeIx;
			for (int i = newFreeIx; i < items.Length;)
			{
				int j = i++;
				items[j].rnext = items[j].lnext = (i < items.Length ? i : -1);
				items[j].rprev = items[j].lprev = (j == newFreeIx ? -1 : j - 1);
			}
			lefthash = null;
			righthash = null;
			Rehash(false);
		}

		public void Rehash(bool newHashes)
		{
			lefthash = new int[items.Length];
			righthash = new int[items.Length];
			lefthash.Fill(-1);
			righthash.Fill(-1);
			for (int i = 0; i < items.Length; ++i)
			{
				if (!items[i].item.HasValue)
					continue;
				int lh = items[i].hash.Left;
				int rh = items[i].hash.Right;
				if (newHashes)
				{
					var p = items[i].item.Value;
					lh = leftcmp.GetHashCode(p.Left);
					rh = rightcmp.GetHashCode(p.Right);
					items[i].hash = new KeyPair<int, int>(lh, rh);
				}
				int lhix = lefthash.GetFromHash(lh);
				int rhix = righthash.GetFromHash(rh);
				items[i].lprev = items[i].rprev = -1;
				items[i].lnext = lhix;
				if (lhix >= 0)
					items[lhix].lprev = i;
				lefthash.SetFromHash(lh, i);
				items[i].rnext = rhix;
				if (rhix >= 0)
					items[rhix].rprev = i;
				righthash.SetFromHash(rh, i);
			}
		}

		private int FindLeft(TLeft key)
		{
			int hsh = leftcmp.GetHashCode(key);
			int ix = lefthash.GetFromHash(hsh);
			while (ix != -1)
			{
				Debug.Assert(items[ix].item.HasValue);
				if (leftcmp.Equals(items[ix].item.Value.Left, key))
					return ix;
				else
					ix = items[ix].lnext;
			}
			return -1;
		}

		private int FindLeftNext(int ix)
		{
			int s;
			for (s = ix; ix != -1; ix = items[ix].lnext)
			{
				Debug.Assert(items[ix].item.HasValue);
				if (leftcmp.Equals(items[ix].item.Value.Left, items[s].item.Value.Left))
					return ix;
			}
			return -1;
		}

		private int FindRight(TRight key)
		{
			int hsh = rightcmp.GetHashCode(key);
			int ix = righthash.GetFromHash(hsh);
			while (ix != -1)
			{
				Debug.Assert(items[ix].item.HasValue);
				if (rightcmp.Equals(items[ix].item.Value.Right, key))
					return ix;
				else
					ix = items[ix].rnext;
			}
			return -1;
		}

		private int FindRightNext(int ix)
		{
			int s;
			for (s = ix; ix != -1; ix = items[ix].rnext)
			{
				Debug.Assert(items[ix].item.HasValue);
				if (rightcmp.Equals(items[ix].item.Value.Right, items[s].item.Value.Right))
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
			leftcmp = leftComparer;
			rightcmp = rightComparer;
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
			if (items[ix].lprev < 0)
			{
				int lh = items[ix].hash.Left;
				Debug.Assert(lefthash.GetFromHash(lh) == ix);
				lefthash.SetFromHash(lh, items[ix].lnext);
			}
			else
			{
				items[items[ix].lprev].lnext = items[ix].lnext;
			}
			if (items[ix].lnext >= 0)
				items[items[ix].lnext].lprev = items[ix].lprev;
			if (items[ix].rprev < 0)
			{
				int rh = items[ix].hash.Right;
				Debug.Assert(righthash.GetFromHash(rh) == ix);
				righthash.SetFromHash(rh, items[ix].rnext);
			}
			else
			{
				items[items[ix].rprev].rnext = items[ix].rnext;
			}
			if (items[ix].rnext >= 0)
				items[items[ix].rnext].rprev = items[ix].rprev;
			items[ix].lnext = freeIndex;
			items[ix].rnext = freeIndex;
			items[ix].lprev = items[ix].rprev = -1;
			items[ix].item = null;
			items[freeIndex].lprev = ix;
			items[freeIndex].rprev = ix;
			freeIndex = ix;
			++freeCount;
		}

		private void Insert(TLeft left, TRight right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			if (freeCount < 1)
				Realloc(items.Length + 1);
			int lh = leftcmp.GetHashCode(left);
			int rh = rightcmp.GetHashCode(right);
			int lhix = lefthash.GetFromHash(lh);
			int rhix = righthash.GetFromHash(rh);
			Debug.Assert(lhix < 0 || items[lhix].lprev == -1);
			Debug.Assert(rhix < 0 || items[rhix].rprev == -1);
			int ix = freeIndex;
			freeIndex = items[ix].lnext;
			items[ix].lprev = items[ix].rprev = -1;
			if (freeIndex >= 0)
				items[freeIndex].lprev = items[freeIndex].rprev = -1;
			--freeCount;
			items[ix].lnext = lhix;
			if (lhix >= 0)
				items[lhix].lprev = ix;
			items[ix].rnext = rhix;
			if (rhix >= 0)
				items[rhix].rprev = ix;
			lefthash.SetFromHash(lh, ix);
			righthash.SetFromHash(rh, ix);
			items[ix].hash = new KeyPair<int, int>(lh, rh);
			items[ix].item = new KeyPair<TLeft, TRight>(left, right);
			++version;
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
				return items[ix].item.Value.Left;
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
				return items[ix].item.Value.Right;
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
				return items.Length - freeCount;
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
			internal Multibimap<TLeft, TRight> instance;
			internal bool useKey;
			internal TRight key;

			public int Count
			{
				get
				{
					if (useKey)
						return instance.Count;
					else
					{

						int ct = 0;
						for (int ix = instance.FindRight(key); ix >= 0; ix = instance.FindRightNext(ix))
						{
							Debug.Assert(instance.items[ix].item.HasValue);
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
					return !useKey || instance.IsReadOnly;
				}
			}

			public void Add(TLeft item)
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(useKey);
				instance.Add(item, key);
			}

			public void Clear()
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(useKey);
				instance.Remove(key);
			}

			public bool Contains(TLeft item)
			{
				if (useKey)
					return instance.Contains(item, key);
				else
					return instance.ContainsKey(item);
			}

			public void CopyTo(TLeft[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < Count)
					throw new ArgumentException("Not enough space in array.");
				if (useKey)
				{
					for (int ix = instance.FindRight(key); ix >= 0; ix = instance.FindRightNext(ix))
						array[arrayIndex++] = instance.items[ix].item.Value.Left;
				}
				else
				{
					for (int i = 0; i < instance.items.Length; ++i)
					{
						if (!instance.items[i].item.HasValue)
							continue;
						array[arrayIndex++] = instance.items[i].item.Value.Left;
					}
				}
			}

			public IEnumerator<TLeft> GetEnumerator()
			{
				if (useKey)
				{
					for (int ix = instance.FindRight(key); ix >= 0; ix = instance.FindRightNext(ix))
						yield return instance.items[ix].item.Value.Left;
				}
				else
				{
					foreach (var kp in instance)
						yield return kp.Left;
				}
			}

			public bool Remove(TLeft item)
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(useKey);
				return instance.Remove(item, key);
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
				return new LeftCollection { instance = this, useKey = false };
			}
		}

		internal class RightCollection : ICollection<TRight>
		{
			internal Multibimap<TLeft, TRight> instance;
			internal bool useKey;
			internal TLeft key;


			public int Count
			{
				get
				{
					if (useKey)
						return instance.Count;
					else
					{

						int ct = 0;
						for (int ix = instance.FindLeft(key); ix >= 0; ix = instance.FindLeftNext(ix))
						{
							Debug.Assert(instance.items[ix].item.HasValue);
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
					return !useKey || instance.IsReadOnly;
				}
			}

			public void Add(TRight item)
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(useKey);
				instance.Add(key, item);
			}

			public void Clear()
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(useKey);
				instance.Remove(key);
			}

			public bool Contains(TRight item)
			{
				if (useKey)
					return instance.Contains(key, item);
				else
					return instance.ContainsKey(item);
			}

			public void CopyTo(TRight[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < Count)
					throw new ArgumentException("Not enough space in array.");
				if (useKey)
				{
					for (int ix = instance.FindLeft(key); ix >= 0; ix = instance.FindLeftNext(ix))
						array[arrayIndex++] = instance.items[ix].item.Value.Right;
				}
				else
				{
					for (int i = 0; i < instance.items.Length; ++i)
					{
						if (!instance.items[i].item.HasValue)
							continue;
						array[arrayIndex++] = instance.items[i].item.Value.Right;
					}
				}
			}

			public IEnumerator<TRight> GetEnumerator()
			{
				if (useKey)
				{
					for (int ix = instance.FindLeft(key); ix >= 0; ix = instance.FindLeftNext(ix))
						yield return instance.items[ix].item.Value.Right;
				}
				else
				{
					foreach (var kp in instance)
						yield return kp.Right;
				}
			}

			public bool Remove(TRight item)
			{
				if (IsReadOnly)
					throw new NotSupportedException("This collection is read-only.");
				Debug.Assert(useKey);
				return instance.Remove(key, item);
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
				return new RightCollection { instance = this, useKey = false };
			}
		}

		public ICollection<TLeft> this[TRight key]
		{
			get
			{
				return new LeftCollection { instance = this, useKey = true, key = key };
			}

			set
			{
				if (value == null)
				{
					Remove(key);
					return;
				}
				TLeft[] l = value.NoneOrThrow((o) => o == null, new Lazy<InvalidOperationException>(() => new InvalidOperationException("The collection may not contain null items."))).ToArray();
				Remove(key);
				foreach (TLeft v in l)
					Add(v, key);
			}
		}

		public ICollection<TRight> this[TLeft key]
		{
			get
			{
				return new RightCollection { instance = this, useKey = true, key = key };
			}

			set
			{
				if (value == null)
				{
					Remove(key);
					return;
				}
				TRight[] l = value.NoneOrThrow((o) => o == null, new Lazy<InvalidOperationException>(() => new InvalidOperationException("The collection may not contain null items."))).ToArray();
				Remove(key);
				foreach (TRight v in l)
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
			if (freeCount < needed)
				Realloc((this.items.Length - freeCount) + needed);
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
			if (freeCount < needed)
				Realloc((this.items.Length - freeCount) + needed);
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
			for (int i = 0; i < items.Length;)
			{
				int j = i++;
				items[j].rnext = items[j].lnext = (i < items.Length ? i : -1);
				items[j].hash = new KeyPair<int, int>(0, 0);
				items[j].item = null;
			}
			freeIndex = 0;
			freeCount = items.Length;
			lefthash.Fill(-1);
			righthash.Fill(-1);
			++version;
		}

		public bool Contains(KeyPair<TLeft, TRight> item)
		{
			for (int ix = FindLeft(item.Left); ix >= 0; ix = FindLeftNext(ix))
			{
				if (rightcmp.Equals(item.Right, items[ix].item.Value.Right))
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
			if ((array.Length - arrayIndex) < Count)
				throw new ArgumentException("Not enough space in array.");
			for (int ix = 0; ix < items.Length; ++ix)
			{
				if (!items[ix].item.HasValue)
					continue;
				array[arrayIndex++] = items[ix].item.Value;
			}
		}

		public IEnumerator<KeyPair<TLeft, TRight>> GetEnumerator()
		{
			int _ver = version;
			for (int ix = 0; ix < items.Length; ++ix)
			{
				if (_ver != version)
					throw new InvalidOperationException("The dictionary has been modified, so the enumerator is now invalid.");
				if (!items[ix].item.HasValue)
					continue;
				yield return items[ix].item.Value;
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
				++version;
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
				++version;
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
			Debug.Assert(items[ix].item.HasValue);
			if (FindRightNext(ix) >= 0)
			{
				value = default(TLeft);
				return false;
			}
			value = items[ix].item.Value.Left;
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
			Debug.Assert(items[ix].item.HasValue);
			if (FindLeftNext(ix) >= 0)
			{
				value = default(TRight);
				return false;
			}
			value = items[ix].item.Value.Right;
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
				if (rightcmp.Equals(right, items[ix].item.Value.Right))
				{
					Unlink(ix);
					++version;
					return true;
				}
			}
			return false;
		}

		public bool Contains(TLeft left, TRight right)
		{
			for (int ix = FindLeft(left); ix >= 0; ix = FindLeftNext(ix))
			{
				if (rightcmp.Equals(right, items[ix].item.Value.Right))
				{
					return true;
				}
			}
			return false;
		}
	}
}
