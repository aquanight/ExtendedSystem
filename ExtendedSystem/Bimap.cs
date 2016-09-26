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
		private TLeftKey left;
		private TRightKey right;

		public KeyPair(TLeftKey left, TRightKey right)
		{
			this.left = left;
			this.right = right;
		}

		public TLeftKey Left
		{
			get
			{
				return left;
			}

		}

		public TRightKey Right
		{
			get
			{
				return right;
			}
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
			internal KeyPair<TLeft, TRight>? item; // null if this is a free entry
			internal KeyPair<int, int> hash; // Invalid if !item.HasValue
							 // If this is a "free" entry, lnext == rnext
			internal int lnext; // -1 if this is the last of the chain
			internal int rnext; // -1 if this is the last of the chain
			internal int lprev; // -1 if this is the first of the chain, -1 for free entries
			internal int rprev; // -1 if this is the first of the chain, -1 for free entries
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
					newItems[newFreeIx].lnext = -1;
					newItems[newFreeIx].rnext = -1;
					newItems[newFreeIx].rprev = -1;
					newItems[newFreeIx].rnext = -1;
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
			items = newItems;
			freeCount = newFreeCt;
			freeIndex = newFreeIx;
			for (int i = newFreeIx; i < items.Length;)
			{
				int j = i++;
				items[j].rnext = items[j].lnext = (i < items.Length ? i : -1);
				items[j].lprev = items[j].rprev = (j == newFreeIx ? -1 : j - 1);
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
				{
					Debug.Assert(items[lhix].lprev == -1 || items[lhix].lprev == i);
					items[lhix].lprev = i;
				}
				lefthash.SetFromHash(lh, i);
				items[i].rnext = rhix;
				if (rhix >= 0)
				{
					Debug.Assert(items[rhix].rprev == -1 || items[rhix].rprev == i);
					items[rhix].rprev = i;
				}
				righthash.SetFromHash(rh, i);
			}
			CheckIntegrity();
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

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
		[Conditional("DEBUG")]
		private void CheckIntegrity()
		{
			if (!Debugger.IsAttached)
				return;
			int failed = 0;
			Debug.Assert(items != null, "Integrity checking failed because the array is null");
			Debug.Assert(lefthash != null, "Integrity checking failed because the array is null");
			Debug.Assert(righthash != null, "Integrity checking failed because the array is null");
			Debug.Assert(items.Length == lefthash.Length && items.Length == righthash.Length, "Integrity checking failed because the arrays aren't congruent");
			if (freeIndex >= 0)
			{
				if (items[freeIndex].item.HasValue)
				{
					Trace.TraceError("At {0}: bad free: item is head of the free chain but it has a value", freeIndex);
					++failed;
				}
				if (items[freeIndex].lprev != -1)
				{
					Trace.TraceError("At {0}: bad linkage: item is head of the free chain but it has a previous link to {1}", freeIndex, items[freeIndex].lprev);
					++failed;
				}
			}
			else
			{
				if (freeCount != 0)
				{
					Trace.TraceError("missing chain: we have a nonzero free count but no link to the free chain");
					++failed;
				}
			}
			for (int i = 0; i < items.Length; ++i)
			{
				// Check the left hash value
				if (lefthash[i] >= 0)
				{
					if (!items[lefthash[i]].item.HasValue)
					{
						Trace.TraceError("At {1}: item head of left hash chain {0} but it has no value", i, lefthash[i]);
						++failed;
					}
					if (items[lefthash[i]].lprev >= 0)
					{
						Trace.TraceError("At {1}: item head of left hash chain {0} has a left-previous linkage to {2}", i, lefthash[i], items[lefthash[i]].lprev);
						++failed;
					}
				}
				// Check the right hash value
				if (righthash[i] >= 0)
				{
					if (!items[righthash[i]].item.HasValue)
					{
						Trace.TraceError("At {1}: item head of right hash chain {0} but it has no value", i, lefthash[i]);
						++failed;
					}
					if (items[righthash[i]].rprev >= 0)
					{
						Trace.TraceError("At {1}: item head of right hash chain {0} has a right-previous linkage to {2}", i, lefthash[i], items[lefthash[i]].rprev);
						++failed;
					}
				}
				int ln = items[i].lnext;
				int rn = items[i].rnext;
				int lp = items[i].lprev;
				int rp = items[i].rprev;
				int lh = items[i].hash.Left;
				int rh = items[i].hash.Right;
				if (items[i].item.HasValue)
				{
					int vlh = leftcmp.GetHashCode(items[i].item.Value.Left);
					if (lh != vlh)
					{
						Trace.TraceError("At {0}: bad hash: the item's left hash is incorrect (expected: {1}, actual {2}", i, lh, vlh);
						++failed;
					}
					int vrh = rightcmp.GetHashCode(items[i].item.Value.Right);
					if (rh != vrh)
					{
						Trace.TraceError("At {0}: bad hash: the item's right hash is incorrect (expected: {1}, actual {2}", i, rh, vrh);
						++failed;
					}
					if (ln >= 0)
					{
						if ((lh - items[ln].hash.Left) % items.Length != 0)
						{
							Trace.TraceError("At {0}: bad hash linkage: the item's left hash is not congruent (modulo length) to its next link", i);
							++failed;
						}
						if (ln == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's left-next links back on itself", i);
							++failed;
						}
						if (!items[ln].item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-next is {1} which is a free entry", i, ln);
							++failed;
						}
						if (items[ln].lprev != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-next linkage points to {1}, but that item points to {2}", i, lp, items[lp].lprev);
							++failed;
						}
					}
					if (rn >= 0)
					{
						if ((rh - items[rn].hash.Right) % items.Length != 0)
						{
							Trace.TraceError("At {0}: bad hash linkage: the item's right hash is not congruent (modulo length) to its next link", i);
							++failed;
						}
						if (rn == i)
						{
							Trace.TraceError("At {0}: circular linkage: the item's right-next links back on itself", i);
							++failed;
						}
						if (!items[rn].item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: right-next is {1} which is a free entry", i, rn);
							++failed;
						}
						if (items[rn].rprev != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's right-next linkage points to {1}, but that item points to {2}", i, lp, items[rn].rprev);
							++failed;
						}
					}
					if (lp < 0)
					{
						if (!lefthash.Contains(i))
						{
							int actual = lefthash.GetFromHash(lh);
							Trace.TraceError("At {0}: orphaned chain: expected left hash slot is pointing to {1}", i, actual);
							++failed;
						}
					}
					else
					{
						if ((lh - items[lp].hash.Left) % items.Length != 0)
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
						if (!items[lp].item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-previous is {1} which is a free entry", i, lp);
							++failed;
						}
						if (items[lp].lnext != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-previous linkage points to {1}, but that item points to {2}", i, lp, items[lp].lnext);
							++failed;
						}
					}
					if (rp < 0)
					{
						if (!righthash.Contains(i))
						{
							int actual = righthash.GetFromHash(rh);
							Trace.TraceError("At {0}: orphaned chain: expected right hash slot is pointing to {1}", i, actual);
							++failed;
						}
					}
					else
					{
						if ((rh - items[rp].hash.Right) % items.Length != 0)
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
						if (!items[rp].item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: right-previous is {1} which is a free entry", i, rp);
							++failed;
						}
						if (items[rp].rnext != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's right-previous linkage points to {1}, but that item points to {2}", i, lp, items[rp].rnext);
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
						if (items[ln].item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-next is {1} which is not a free entry", i, ln);
							++failed;
						}
						if (items[ln].lprev != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-next linkage points to {1}, but that item points to {2}", i, ln, items[ln].lprev);
							++failed;
						}
					}
					if (lp < 0)
					{
						if (freeIndex != i)
						{
							Trace.TraceError("At {0}: orphaned chain: expected free index is pointing to {1}", i, freeIndex);
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
						if (items[lp].item.HasValue)
						{
							Trace.TraceError("At {0}: bad linkage: left-previous is {1} which is not a free entry", i, lp);
							++failed;
						}
						if (items[lp].lnext != i)
						{
							Trace.TraceError("At {0}: broken linkage: the item's left-previous linkage points to {1}, but that item points to {2}", i, lp, items[lp].lnext);
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
			if (leftComparer == null)
				throw new ArgumentNullException("leftComparer");
			if (rightComparer == null)
				throw new ArgumentNullException("rightComparer");
			leftcmp = leftComparer;
			rightcmp = rightComparer;
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
			CheckIntegrity();
		}

		public TLeft this[TRight key]
		{
			get
			{
				int ix = FindRight(key);
				if (ix < 0)
					throw new KeyNotFoundException();
				return items[ix].item.Value.Left;
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
				return items[ix].item.Value.Right;
			}

			set
			{
				Insert(key, value, false);
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
			internal Bimap<TLeft, TRight> instance;

			public int Count
			{
				get
				{
					return instance.Count;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return true;
				}
			}

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
				return instance.ContainsKey(item);
			}

			public void CopyTo(TLeft[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < Count)
					throw new ArgumentException("Not enough space in array.");
				for (int i = 0; i < instance.items.Length; ++i)
				{
					if (!instance.items[i].item.HasValue)
						continue;
					array[arrayIndex++] = instance.items[i].item.Value.Left;
				}
			}

			public IEnumerator<TLeft> GetEnumerator()
			{
				foreach (var kp in instance)
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

		public ICollection<TLeft> LeftValues
		{
			get
			{
				return new LeftCollection { instance = this };
			}
		}

		internal class RightCollection : ICollection<TRight>
		{
			internal Bimap<TLeft, TRight> instance;

			public int Count
			{
				get
				{
					return instance.Count;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return true;
				}
			}

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
				return instance.ContainsKey(item);
			}

			public void CopyTo(TRight[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				if (array.Length - arrayIndex < Count)
					throw new ArgumentException("Not enough space in array.");
				for (int i = 0; i < instance.items.Length; ++i)
				{
					if (!instance.items[i].item.HasValue)
						continue;
					array[arrayIndex++] = instance.items[i].item.Value.Right;
				}
			}

			public IEnumerator<TRight> GetEnumerator()
			{
				foreach (var kp in instance)
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

		public ICollection<TRight> RightValues
		{
			get
			{
				return new RightCollection { instance = this };
			}
		}

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
			int ix = FindLeft(item.Left);
			if (ix < 0)
				return false;
			return (rightcmp.Equals(item.Right, items[ix].item.Value.Right));
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
			int ix = FindLeft(item.Left);
			if (ix < 0)
				return false;
			if (rightcmp.Equals(item.Right, items[ix].item.Value.Right))
			{
				Unlink(ix);
				++version;
				return true;
			}
			return false;
		}

		public bool Remove(TRight key)
		{
			int ix = FindRight(key);
			if (ix < 0)
				return false;
			++version;
			Unlink(ix);
			return true;
		}

		public bool Remove(TLeft key)
		{
			int ix = FindLeft(key);
			if (ix < 0)
				return false;
			++version;
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
			Debug.Assert(items[ix].item.HasValue);
			value = items[ix].item.Value.Left;
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
			Debug.Assert(items[ix].item.HasValue);
			value = items[ix].item.Value.Right;
			return true;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEqualityComparer<TLeft> LeftComparer
		{
			get
			{
				return leftcmp;
			}
		}

		public IEqualityComparer<TRight> RightComparer
		{
			get
			{
				return rightcmp;
			}
		}

		public int Capacity
		{
			get
			{
				return items.Length;
			}
			set
			{
				Realloc(value);
			}
		}

		public float LoadFactor
		{
			get
			{
				return Count / (float)lefthash.Length;
			}
		}

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
