using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ExtendedSystem
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
	public struct KeyPair<TLeftKey, TRightKey>
	{
		public KeyPair(TLeftKey left, TRightKey right)
		{
			this.Left = left;
			this.Right = right;
		}

		public TLeftKey Left
		{
			get;
		}

		public TRightKey Right
		{
			get;
		}

		
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Bimap")]
	public interface IBimap<TLeftKey, TRightKey> : ICollection<KeyPair<TLeftKey, TRightKey>>, IEnumerable<KeyPair<TLeftKey, TRightKey>>, IEnumerable
	{
		TRightKey this[TLeftKey key] { get; set; }
		TLeftKey this[TRightKey key] { get; set; }
		void Add(TLeftKey left, TRightKey right);
		bool ContainsKey(TLeftKey key);
		bool ContainsKey(TRightKey key);
		bool Remove(TLeftKey key);
		bool Remove(TRightKey key);
		bool TryGetValue(TLeftKey key, out TRightKey value);
		bool TryGetValue(TRightKey key, out TLeftKey value);
		ICollection<TLeftKey> LeftValues
		{
			get;
		}
		ICollection<TRightKey> RightValues
		{
			get;
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Bimap")]
	public class Bimap<TLeft, TRight> : IBimap<TLeft, TRight>
	{
		// Use nullables so that we can use the entire hashcode.
		private struct Entry
		{
			internal KeyPair<TLeft, TRight>? _item; // null if this is a free entry
			internal KeyPair<int, int> _hash; // Invalid if !item.HasValue
							 // If this is a "free" entry, lnext == rnext
			internal int _lnext; // -1 if this is the last of the chain
			internal int _rnext; // -1 if this is the last of the chain
			internal int _lprev; // -1 if this is the first of the chain, -1 for free entries
			internal int _rprev; // -1 if this is the first of the chain, -1 for free entries
		}
		private Entry[] _items;
		private int[] _lefthash; // Empty buckets contain -1
		private int[] _righthash;
		private int _version = 0;
		private int _freeIndex; // -1 : no free entries - next addition will force a reallocation
		private int _freeCount;

		// Performs a reallocation of the entry set.
		// A general condition is that items, lefthash, and righthash are all the same length.
		// desiredCapacity is adjusted to the smallest power of 2 that is greater than or equal to all of:
		// - The supplied value of desiredCapacity
		// - 16
		// - The number of items currently in the dictionary
		// A reallocation is performed if any of these conditions are met:
		// - Any of items, lefthash, or righthash is null, which is the case during construction.
		// - desiredCapacity is greater than the current array length
		// - desiredCapacity is less than half the current array length
		// If the conditions are not met, no reallocation is performed, Realloc does nothing.
		// After reallocation, a rehash occurs.
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
					newItems[newFreeIx]._lnext = -1;
					newItems[newFreeIx]._rnext = -1;
					newItems[newFreeIx]._rprev = -1;
					newItems[newFreeIx]._rnext = -1;
					newFreeIx++;
				}
			}
			//else
			//{
			//	for (int i = 0; i < newItems.Length; ++i)
			//	{
			//		newItems[i].item = null;
			//		newItems[i].lprev = newItems[i].rprev = newItems[i].lnext = newItems[i].rnext = -1;
			//	}
			//}
			this._items = newItems;
			this._freeCount = newFreeCt;
			this._freeIndex = newFreeIx;
			for (int i = newFreeIx; i < this._items.Length;)
			{
				int j = i++;
				this._items[j]._rnext = this._items[j]._lnext = (i < this._items.Length ? i : -1);
				this._items[j]._lprev = this._items[j]._rprev = (j == newFreeIx ? -1 : j - 1);
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
					lh = this.LeftComparer.GetHashCode(p.Left);
					rh = this.RightComparer.GetHashCode(p.Right);
					this._items[i]._hash = new KeyPair<int, int>(lh, rh);
				}
				int lhix = this._lefthash.GetFromHash(lh);
				int rhix = this._righthash.GetFromHash(rh);
				this._items[i]._lprev = this._items[i]._rprev = -1;
				this._items[i]._lnext = lhix;
				if (lhix >= 0)
				{
					Debug.Assert(this._items[lhix]._lprev == -1 || this._items[lhix]._lprev == i);
					this._items[lhix]._lprev = i;
				}
				this._lefthash.SetFromHash(lh, i);
				this._items[i]._rnext = rhix;
				if (rhix >= 0)
				{
					Debug.Assert(this._items[rhix]._rprev == -1 || this._items[rhix]._rprev == i);
					this._items[rhix]._rprev = i;
				}
				this._righthash.SetFromHash(rh, i);
			}
			CheckIntegrity();
		}

		private int FindLeft(TLeft key)
		{
			int hsh = this.LeftComparer.GetHashCode(key);
			int ix = this._lefthash.GetFromHash(hsh);
			while (ix != -1)
			{
				Debug.Assert(this._items[ix]._item.HasValue);
				if (this.LeftComparer.Equals(this._items[ix]._item.Value.Left, key))
					return ix;
				else
					ix = this._items[ix]._lnext;
			}
			return -1;
		}

		private int FindRight(TRight key)
		{
			int hsh = this.RightComparer.GetHashCode(key);
			int ix = this._righthash.GetFromHash(hsh);
			while (ix != -1)
			{
				Debug.Assert(this._items[ix]._item.HasValue);
				if (this.RightComparer.Equals(this._items[ix]._item.Value.Right, key))
					return ix;
				else
					ix = this._items[ix]._rnext;
			}
			return -1;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
		[Conditional("DEBUG")]
		private void CheckIntegrity()
		{
			if (!Debugger.IsAttached)
				return;
			int failed = 0;
			Debug.Assert(this._items != null, "Integrity checking failed because the array is null");
			Debug.Assert(this._lefthash != null, "Integrity checking failed because the array is null");
			Debug.Assert(this._righthash != null, "Integrity checking failed because the array is null");
			Debug.Assert(this._items.Length == this._lefthash.Length && this._items.Length == this._righthash.Length, "Integrity checking failed because the arrays aren't congruent");
			if (this._freeIndex >= 0)
			{
				if (this._items[this._freeIndex]._item.HasValue)
				{
					Trace.TraceError("At {0}: bad free: item is head of the free chain but it has a value", this._freeIndex);
					++failed;
				}
				if (this._items[this._freeIndex]._lprev != -1)
				{
					Trace.TraceError("At {0}: bad linkage: item is head of the free chain but it has a previous link to {1}", this._freeIndex, this._items[this._freeIndex]._lprev);
					++failed;
				}
			}
			else
			{
				if (this._freeCount != 0)
				{
					Trace.TraceError("missing chain: we have a nonzero free count but no link to the free chain");
					++failed;
				}
			}
			for (int i = 0; i < this._items.Length; ++i)
			{
				// Check the left hash value
				if (this._lefthash[i] >= 0)
				{
					if (!this._items[this._lefthash[i]]._item.HasValue)
					{
						Trace.TraceError("At {1}: item head of left hash chain {0} but it has no value", i, this._lefthash[i]);
						++failed;
					}
					if (this._items[this._lefthash[i]]._lprev >= 0)
					{
						Trace.TraceError("At {1}: item head of left hash chain {0} has a left-previous linkage to {2}", i, this._lefthash[i], this._items[this._lefthash[i]]._lprev);
						++failed;
					}
				}
				// Check the right hash value
				if (this._righthash[i] >= 0)
				{
					if (!this._items[this._righthash[i]]._item.HasValue)
					{
						Trace.TraceError("At {1}: item head of right hash chain {0} but it has no value", i, this._lefthash[i]);
						++failed;
					}
					if (this._items[this._righthash[i]]._rprev >= 0)
					{
						Trace.TraceError("At {1}: item head of right hash chain {0} has a right-previous linkage to {2}", i, this._lefthash[i], this._items[this._lefthash[i]]._rprev);
						++failed;
					}
				}
				int ln = this._items[i]._lnext;
				int rn = this._items[i]._rnext;
				int lp = this._items[i]._lprev;
				int rp = this._items[i]._rprev;
				int lh = this._items[i]._hash.Left;
				int rh = this._items[i]._hash.Right;
				if (this._items[i]._item.HasValue)
				{
					int vlh = this.LeftComparer.GetHashCode(this._items[i]._item.Value.Left);
					if (lh != vlh)
					{
						Trace.TraceError("At {0}: bad hash: the item's left hash is incorrect (expected: {1}, actual {2}", i, lh, vlh);
						++failed;
					}
					int vrh = this.RightComparer.GetHashCode(this._items[i]._item.Value.Right);
					if (rh != vrh)
					{
						Trace.TraceError("At {0}: bad hash: the item's right hash is incorrect (expected: {1}, actual {2}", i, rh, vrh);
						++failed;
					}
					if (ln >= 0)
					{
						if ((lh - this._items[ln]._hash.Left) % this._items.Length != 0)
						{
							Trace.TraceError("At {0}: bad hash linkage: the item's left hash is not congruent (modulo length) to its next link", i);
							++failed;
						}
						if (ln == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's left-next links back on itself", i);
							++failed;
						}
						if (!this._items[ln]._item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-next is {1} which is a free entry", i, ln);
							++failed;
						}
						if (this._items[ln]._lprev != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-next linkage points to {1}, but that item points to {2}", i, lp, this._items[lp]._lprev);
							++failed;
						}
					}
					if (rn >= 0)
					{
						if ((rh - this._items[rn]._hash.Right) % this._items.Length != 0)
						{
							Trace.TraceError("At {0}: bad hash linkage: the item's right hash is not congruent (modulo length) to its next link", i);
							++failed;
						}
						if (rn == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's right-next links back on itself", i);
							++failed;
						}
						if (!this._items[rn]._item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: right-next is {1} which is a free entry", i, rn);
							++failed;
						}
						if (this._items[rn]._rprev != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's right-next linkage points to {1}, but that item points to {2}", i, lp, this._items[rn]._rprev);
							++failed;
						}
					}
					if (lp < 0)
					{
						if (!this._lefthash.Contains(i))
						{
							int actual = this._lefthash.GetFromHash(lh);
							Trace.TraceError("At {0}: orphaned chain: expected left hash slot is pointing to {1}", i, actual);
							++failed;
						}
					}
					else
					{
						if ((lh - this._items[lp]._hash.Left) % this._items.Length != 0)
						{
							Trace.TraceError("At {0}: bad hash linkage: the item's left hash is not congruent (modulo length) to its previous link", i);
							++failed;
						}
						if (lp == ln)
						{
							Trace.TraceError("At {0}: circular linkage: the item's left-previous and left-next links point to the same {1}", i, lp);
							++failed;
						}
						if (lp == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's left-previous links back on itself", i);
							++failed;
						}
						if (!this._items[lp]._item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-previous is {1} which is a free entry", i, lp);
							++failed;
						}
						if (this._items[lp]._lnext != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-previous linkage points to {1}, but that item points to {2}", i, lp, this._items[lp]._lnext);
							++failed;
						}
					}
					if (rp < 0)
					{
						if (!this._righthash.Contains(i))
						{
							int actual = this._righthash.GetFromHash(rh);
							Trace.TraceError("At {0}: orphaned chain: expected right hash slot is pointing to {1}", i, actual);
							++failed;
						}
					}
					else
					{
						if ((rh - this._items[rp]._hash.Right) % this._items.Length != 0)
						{
							Trace.TraceError("At {0}: bad hash linkage: the item's right hash is not congruent (modulo length) to its previous link", i);
							++failed;
						}
						if (rp == rn)
						{
							Trace.TraceError("At {0}: circular linkage: the item's right-previous and right-next links point to the same {1}", i, rp);
							++failed;
						}
						if (rp == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's right-previous links back on itself", i);
							++failed;
						}
						if (!this._items[rp]._item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: right-previous is {1} which is a free entry", i, rp);
							++failed;
						}
						if (this._items[rp]._rnext != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's right-previous linkage points to {1}, but that item points to {2}", i, lp, this._items[rp]._rnext);
							++failed;
						}
					}
				}
				else
				{
					if (ln != rn)
					{
						Trace.TraceError("At {0}: free linkage: left and right next links are not equal", i);
						++failed;
					}
					if (lp != rp)
					{
						Trace.TraceError("At {0}: free linkage: left and right next links are not equal", i);
						++failed;
					}
					if (ln >= 0)
					{
						if (ln == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's left-next links back on itself", i);
							++failed;
						}
						if (this._items[ln]._item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-next is {1} which is not a free entry", i, ln);
							++failed;
						}
						if (this._items[ln]._lprev != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-next linkage points to {1}, but that item points to {2}", i, ln, this._items[ln]._lprev);
							++failed;
						}
					}
					if (lp < 0)
					{
						if (this._freeIndex != i)
						{
							Trace.TraceError("At {0}: orphaned chain: expected free index is pointing to {1}", i, this._freeIndex);
							++failed;
						}
					}
					else
					{
						if (lp == ln)
						{
							Trace.TraceError("At {0}: circular linkage: the item's left-previous and left-next links point to the same {1}", i, lp);
							++failed;
						}
						if (lp == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's left-previous links back on itself", i);
							++failed;
						}
						if (this._items[lp]._item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-previous is {1} which is not a free entry", i, lp);
							++failed;
						}
						if (this._items[lp]._lnext != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-previous linkage points to {1}, but that item points to {2}", i, lp, this._items[lp]._lnext);
							++failed;
						}
					}
				}
			}
			Debug.Assert(failed == 0, "The hash integrity is failed.", "{0} failure(s) encountered in checking the table", failed);
		}

		public Bimap() : this(0)
		{
		}

		public Bimap(int capacity) : this(capacity, EqualityComparer<TLeft>.Default, EqualityComparer<TRight>.Default)
		{
		}

		public Bimap(IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer) : this(0, leftComparer, rightComparer)
		{
		}

		public Bimap(int capacity, IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer)
		{
			this.LeftComparer = leftComparer ?? throw new ArgumentNullException("leftComparer");
			this.RightComparer = rightComparer ?? throw new ArgumentNullException("rightComparer");
			Realloc(capacity);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Bimap(ICollection<KeyPair<TLeft, TRight>> source) : this(source, EqualityComparer<TLeft>.Default, EqualityComparer<TRight>.Default)
		{
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Bimap(ICollection<KeyPair<TLeft, TRight>> source, IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer) : this(0, leftComparer, rightComparer)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			AddRange(source);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Bimap(ICollection<KeyValuePair<TLeft, TRight>> source) : this(source, EqualityComparer<TLeft>.Default, EqualityComparer<TRight>.Default)
		{
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public Bimap(ICollection<KeyValuePair<TLeft, TRight>> source, IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer) : this(0, leftComparer, rightComparer)
		{
			if (source == null)
				throw new ArgumentNullException("source");
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
			CheckIntegrity();
		}

		private void Insert(TLeft left, TRight right, bool adding)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			int lix = FindLeft(left);
			int rix = FindRight(right);
			if (lix >= 0)
			{
				if (adding)
					throw new ArgumentException("The left key is already in this map.", "left");
				else
					Unlink(lix);
			}
			if (rix >= 0)
			{
				if (adding)
					throw new ArgumentException("The right key is already in this map.", "right");
				else
					Unlink(rix);
			}
			if (this._freeCount < 1)
				Realloc(this._items.Length + 1);
			int lh = this.LeftComparer.GetHashCode(left);
			int rh = this.RightComparer.GetHashCode(right);
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
			CheckIntegrity();
		}

		public TLeft this[TRight key]
		{
			get
			{
				int ix = FindRight(key);
				if (ix < 0)
					throw new KeyNotFoundException();
				return this._items[ix]._item.Value.Left;
			}

			set
			{
				Insert(value, key, false);
			}
		}

		public TRight this[TLeft key]
		{
			get
			{
				int ix = FindLeft(key);
				if (ix < 0)
					throw new KeyNotFoundException();
				return this._items[ix]._item.Value.Right;
			}

			set
			{
				Insert(key, value, false);
			}
		}

		public int Count => this._items.Length - this._freeCount;

		public bool IsReadOnly => false;

		internal class LeftCollection : ICollection<TLeft>
		{
			internal Bimap<TLeft, TRight> _instance;

			public int Count => this._instance.Count;

			public bool IsReadOnly => true;

			public void Add(TLeft item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public void Clear()
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public bool Contains(TLeft item)
			{
				return this._instance.ContainsKey(item);
			}

			public void CopyTo(TLeft[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < this.Count)
					throw new ArgumentException("Not enough space in array.");
				for (int i = 0; i < this._instance._items.Length; ++i)
				{
					if (!this._instance._items[i]._item.HasValue)
						continue;
					array[arrayIndex++] = this._instance._items[i]._item.Value.Left;
				}
			}

			public IEnumerator<TLeft> GetEnumerator()
			{
				foreach (var kp in this._instance)
					yield return kp.Left;
			}

			public bool Remove(TLeft item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public ICollection<TLeft> LeftValues => new LeftCollection { _instance = this };

		internal class RightCollection : ICollection<TRight>
		{
			internal Bimap<TLeft, TRight> _instance;

			public int Count => this._instance.Count;

			public bool IsReadOnly => true;

			public void Add(TRight item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public void Clear()
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public bool Contains(TRight item)
			{
				return this._instance.ContainsKey(item);
			}

			public void CopyTo(TRight[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < this.Count)
					throw new ArgumentException("Not enough space in array.");
				for (int i = 0; i < this._instance._items.Length; ++i)
				{
					if (!this._instance._items[i]._item.HasValue)
						continue;
					array[arrayIndex++] = this._instance._items[i]._item.Value.Right;
				}
			}

			public IEnumerator<TRight> GetEnumerator()
			{
				foreach (var kp in this._instance)
					yield return kp.Right;
			}

			public bool Remove(TRight item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public ICollection<TRight> RightValues => new RightCollection { _instance = this };

		public void Add(KeyPair<TLeft, TRight> item)
		{
			Add(item.Left, item.Right);
		}

		public void Add(TLeft left, TRight right)
		{
			Insert(left, right, true);
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
			int ix = FindLeft(item.Left);
			if (ix < 0)
				return false;
			return (this.RightComparer.Equals(item.Right, this._items[ix]._item.Value.Right));
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
			int ix = FindLeft(item.Left);
			if (ix < 0)
				return false;
			if (this.RightComparer.Equals(item.Right, this._items[ix]._item.Value.Right))
			{
				Unlink(ix);
				++this._version;
				return true;
			}
			return false;
		}

		public bool Remove(TRight key)
		{
			int ix = FindRight(key);
			if (ix < 0)
				return false;
			++this._version;
			Unlink(ix);
			return true;
		}

		public bool Remove(TLeft key)
		{
			int ix = FindLeft(key);
			if (ix < 0)
				return false;
			++this._version;
			Unlink(ix);
			return true;
		}

		public bool TryGetValue(TRight key, out TLeft value)
		{
			int ix = FindRight(key);
			if (ix < 0)
			{
				value = default(TLeft);
				return false;
			}
			Debug.Assert(this._items[ix]._item.HasValue);
			value = this._items[ix]._item.Value.Left;
			return true;
		}

		public bool TryGetValue(TLeft key, out TRight value)
		{
			int ix = FindLeft(key);
			if (ix < 0)
			{
				value = default(TRight);
				return false;
			}
			Debug.Assert(this._items[ix]._item.HasValue);
			value = this._items[ix]._item.Value.Right;
			return true;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEqualityComparer<TLeft> LeftComparer
		{
			get;
		}

		public IEqualityComparer<TRight> RightComparer
		{
			get;
		}

		public int Capacity
		{
			get
			{
				return this._items.Length;
			}
			set
			{
				Realloc(value);
			}
		}

		public float LoadFactor => this.Count / (float)this._lefthash.Length;

		/*
		public Bimap<TRight, TLeft> Invert()
		{
			Bimap<TRight, TLeft> bm = new Bimap<TRight, TLeft>();
			bm.items = new Bimap<TRight, TLeft>.Entry[items.Length];
			for (int i = 0; i < items.Length; ++i)
			{
				bm.items[i] = new Bimap<TRight, TLeft>.Entry
				{
					item = items[i].item.HasValue ? new KeyPair<TRight, TLeft>(items[i].item.Value.Right, items[i].item.Value.Left) : (KeyPair<TRight, TLeft>?)null,
					hash = new KeyPair<int, int>(items[i].hash.Right, items[i].hash.Left),
					lnext = items[i].rnext,
					lprev = items[i].rprev,
					rnext = items[i].lnext,
					rprev = items[i].rprev
				};
			}
			bm.leftcmp = rightcmp;
			bm.rightcmp = leftcmp;
			bm.lefthash = righthash.Duplicate();
			bm.righthash = lefthash.Duplicate();
			bm.freeCount = freeCount;
			bm.freeIndex = freeIndex;
			return bm;
		}
		*/
	}
}
