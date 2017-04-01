using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	public delegate void RefAction<T>(ref T value);

	public delegate TReturn RefFunc<T, out TReturn>(ref T value);

	public delegate bool RefPredicate<T>(ref T value);

	/// <summary>
	/// Similar to List&lt;T&gt; except that it has mechanisms to acquire references to the elements of the list (rather than copies of them). In the case of
	/// value types, this allows the contents of those types to be modified in-place.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	public sealed class AddressableList<T> : IList<T>
	{
		private T[] _data;

		/// <summary>
		/// Used for locking between Resize and GetRef. This does not guarantee thread safety!! It is used to block resize attempts while there's
		/// an active reference, and to block getting references while a resize is happening.
		/// The only reason it's even needed is because GetRef is considered re-entrant (because it calls a user-supplied callback).
		/// Resize operations are not, but it does provide at least *some* protection from threading.
		/// </summary>
		private int _state;

		/// <summary>
		/// Delegate which takes a reference to the array elements.
		/// </summary>
		/// <param name="value"></param>
		public AddressableList() : this(0)
		{
		}

		public AddressableList(int initialCapacity)
		{
			this._state = 0;
			this.Count = 0;
			Resize(initialCapacity);
		}

		public AddressableList(IEnumerable<T> collection) : this(0)
		{
			AddRange(collection);
		}

		/// <summary>
		/// Attempt to enter the internal lock for resizing.
		/// If *and only if* this is successful, you must ensure a call to ExitResize.
		/// 
		/// </summary>
		/// <returns></returns>
		private bool TryEnterResize()
		{
			int state = Interlocked.Decrement(ref this._state);
			if (state < 0)
				return true;
			Interlocked.Increment(ref this._state);
			return false;
		}

		private void ExitResize()
		{
			Interlocked.Increment(ref this._state);
		}

		/// <summary>
		/// Attempt to enter the internal lock for getting reference.
		/// If *and only if* successful, you must ensure a call to ExitReference.
		/// </summary>
		/// <returns></returns>
		private bool TryEnterReference()
		{
			int state = Interlocked.Increment(ref this._state);
			if (state > 0)
				return true;
			Interlocked.Decrement(ref this._state);
			return false;
		}

		private void ExitReference()
		{
			Interlocked.Decrement(ref this._state);
		}

		/// <summary>
		/// Attempt to resize the data buffer. If an active reference is live, it will throw an exception.
		/// </summary>
		/// <param name="newCapacity"></param>
		private void Resize(int newCapacity)
		{
			if (newCapacity < this.Count)
				throw new ArgumentOutOfRangeException(nameof(newCapacity));
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot modify Capacity while there is an active element reference.");
			try
			{
				var newData = (newCapacity == 0 ? Array.Empty<T>() : new T[newCapacity]);
				for (int i = 0; i < this.Count; ++i)
					newData[i] = this._data[i];
				this._data = newData;
			}
			finally
			{
				ExitResize();
			}
		}

		public T this[int index]
		{
			get
			{
				if (index < 0 || index >= this._data.Length)
					throw new ArgumentOutOfRangeException(nameof(index));
				return this._data[index];
			}
			set
			{
				if (index < 0 || index >= this._data.Length)
					throw new ArgumentOutOfRangeException(nameof(index));
				this._data[index] = value;
			}
		}

		/// <summary>
		/// Acquires a reference to a selected array element for use by the caller. The reference can only be guaranteed until the next call to Insert, InsertRange, Remove, RemoveRange, Clear, or
		/// any assignment to the Capacity property, or any call to Add or AddRange that requires Capacity to change.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		[System.Security.SecuritySafeCritical()]
		public ref T Address(int index)
		{
			if (index < 0 || index >= this._data.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return ref this._data[index];
		}

		/// <summary>
		/// Calls action, passing an element of the list by reference. The called action can then modify fields or call methods against the actual stored
		/// element, instead of retrieving a copy of it with the standard indexer.
		/// The delegate called (and also any other threads that might try to access the list while action is active) cannot make calls to Insert, InsertRange,
		/// Remove, RemoveRange, or Clear, nor can it modify the Capacity property, or make any call to Add or AddRange that requires Capacity to be changed.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="action"></param>
		public void GetRef(int index, RefAction<T> action)
		{
			if (index < 0 || index >= this.Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			// Arguably we can just block the thread, since resizing is not re-entrant. Oh well.
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference while a resize in progress.");
			try
			{
				action(ref this._data[index]);
			}
			finally
			{
				ExitReference();
			}
		}

		/// <summary>
		/// Calls func, passing an element of the list by reference. The called func can then modify fields or call methods against the actual stored
		/// element, instead of retrieving a copy of it with the standard indexer.
		/// The delegate called (and also any other threads that might try to access the list while func is active) cannot make calls to Insert, InsertRange,
		/// Remove, RemoveRange, or Clear, nor can it modify the Capacity property, or make any call to Add or AddRange that requires Capacity to be changed.
		/// Returns the value that func returns.
		/// WARNING: It may be possible to return an inner delegate instance that captures the reference in a closure. If you do this, there is no guarantee
		/// the reference remains safe to use following any Clear, Insert*, Remove*, Add, or assignment to Capacity: attempts to access the reference in such
		/// circumstances may lead to undefined behavior.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="index"></param>
		/// <param name="func"></param>
		/// <returns></returns>
		public TResult GetRef<TResult>(int index, RefFunc<T, TResult> func)
		{
			if (index < 0 || index >= this.Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (func == null)
				throw new ArgumentNullException(nameof(func));
			// Arguably we can just block the thread, since resizing is not re-entrant. Oh well.
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference while a resize in progress.");
			try
			{
				return func(ref this._data[index]);
			}
			finally
			{
				ExitReference();
			}
		}

		public int Capacity
		{
			get
			{
				return this._data.Length;
			}
			set
			{
				Resize(value);
			}
		}

		public int Count
		{
			get;
			private set;
		}

		public bool IsReadOnly => false;

		public void Add(T item)
		{
			if (this.Count >= this._data.Length)
				Resize(this.Count + 1);
			this._data[this.Count++] = item;
		}

		public void AddRange(IEnumerable<T> collection)
		{
			var incoming = collection.ToArray();
			if ((this.Count + incoming.Length) > this._data.Length)
				Resize(this.Count + incoming.Length);
			incoming.CopyTo<T>(this._data, this.Count);
			this.Count += incoming.Length;
		}

		public ReadOnlyCollection<T> AsReadOnly()
		{
			return new ReadOnlyCollection<T>(this);
		}

		public int BinarySearch(T item)
		{
			return BinarySearch(0, this.Count, item, null);
		}

		public int BinarySearch(T item, IComparer<T> comparer)
		{
			return BinarySearch(0, this.Count, item, comparer);
		}

		public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("Specified range is invalid");
			if (comparer == null)
				comparer = Comparer<T>.Default;
			return Array.BinarySearch(this._data, index, count, item, comparer);
		}

		public void Clear()
		{
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot clear the collection while there is an active reference.");
			try
			{
				for (int i = 0; i < this.Count; ++i)
					this._data[i] = default(T);
				this.Count = 0;
			}
			finally
			{
				ExitResize();
			}
		}

		public bool Contains(T item)
		{
			var eq = EqualityComparer<T>.Default;
			for (int i = 0; i < this.Count; ++i)
				if (eq.Equals(this._data[i], item))
					return true;
			return false;
		}

		public AddressableList<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
		{
			if (converter == null)
				throw new ArgumentNullException(nameof(converter));
			var output = new AddressableList<TOutput>(this.Capacity)
			{
				Count = this.Count
			};
			for (int i = 0; i < this.Count; ++i)
				output._data[i] = converter(this._data[i]);
			return output;
		}

		public void CopyTo(T[] array)
		{
			CopyTo(array, 0);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			CopyTo(0, array, arrayIndex, this.Count);
		}

		public void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("The source range is invalid.");
			if (arrayIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(arrayIndex));
			if (arrayIndex + count > array.Length)
				throw new ArgumentException("The destination range is invalid.");
			for (int i = 0; i < count; ++i)
				array[arrayIndex + i] = this._data[index + i];
		}

		public bool Exists(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			for (int i = 0; i < this.Count; ++i)
				if (predicate(this._data[i]))
					return true;
			return false;
		}

		public bool Exists(RefFunc<T, bool> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference while a resize in progress.");
			try
			{
				for (int i = 0; i < this.Count; ++i)
					if (predicate(ref this._data[i]))
						return true;
				return false;
			}
			finally
			{
				ExitReference();
			}
		}

		public T Find(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			for (int i = 0; i < this.Count; ++i)
				if (predicate(this._data[i]))
					return this._data[i];
			return default(T);
		}

		public AddressableList<T> FindAll(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			var result = new AddressableList<T>(this.Capacity);
			for (int i = 0; i < this.Count; ++i)
			{
				if (predicate(this._data[i]))
					result._data[result.Count++] = this._data[i];
			}
			return result;
		}

		public int FindIndex(Predicate<T> predicate)
		{
			return FindIndex(0, this.Count, predicate);
		}

		public int FindIndex(int index, Predicate<T> predicate)
		{
			return FindIndex(index, this.Count - index, predicate);
		}

		public int FindIndex(int index, int count, Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("The search range is invalid.");
			for (int i = 0; i < count; ++i)
			{
				if (predicate(this._data[index + i]))
					return index + i;
			}
			return -1;
		}

		public int FindIndex(RefFunc<T, bool> predicate)
		{
			return FindIndex(0, this.Count, predicate);
		}

		public int FindIndex(int index, RefFunc<T, bool> predicate)
		{
			return FindIndex(index, this.Count - index, predicate);
		}

		public int FindIndex(int index, int count, RefFunc<T, bool> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("The search range is invalid.");
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference during a resize operation.");
			try
			{
				for (int i = 0; i < count; ++i)
				{
					if (predicate(ref this._data[index + i]))
						return index + i;
				}
				return -1;
			}
			finally
			{
				ExitReference();
			}
		}

		public T FindLast(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			for (int i = this.Count; i > 0; --i)
				if (predicate(this._data[i - 1]))
					return this._data[i - 1];
			return default(T);
		}

		public int FindLastIndex(Predicate<T> predicate)
		{
			return FindLastIndex(0, this.Count, predicate);
		}

		public int FindLastIndex(int index, Predicate<T> predicate)
		{
			return FindLastIndex(index, this.Count - index, predicate);
		}

		public int FindLastIndex(int index, int count, Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("The search range is invalid.");
			for (int i = count; i > 0; --i)
			{
				if (predicate(this._data[index + i - 1]))
					return index + i - 1;
			}
			return -1;
		}

		public int FindLastIndex(RefFunc<T, bool> predicate)
		{
			return FindLastIndex(0, this.Count, predicate);
		}

		public int FindLastIndex(int index, RefFunc<T, bool> predicate)
		{
			return FindLastIndex(index, this.Count - index, predicate);
		}

		public int FindLastIndex(int index, int count, RefFunc<T, bool> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("The search range is invalid.");
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference during a resize operation.");
			try
			{
				for (int i = count; i > 0; --i)
				{
					if (predicate(ref this._data[index + i - 1]))
						return index + i - 1;
				}
				return -1;
			}
			finally
			{
				ExitReference();
			}
		}

		public void ForEach(Action<T> action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			for (int i = 0; i < this.Count; ++i)
				action(this._data[i]);
		}

		public void ForEach(RefAction<T> action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference during a resize operation.");
			try
			{
				for (int i = 0; i < this.Count; ++i)
					action(ref this._data[i]);
			}
			finally
			{
				ExitReference();
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			for (int i = 0; i < this.Count; ++i)
				yield return this._data[i];
		}

		public int IndexOf(T item)
		{
			return IndexOf(item, 0, this.Count);
		}

		public int IndexOf(T item, int index)
		{
			return IndexOf(item, index, this.Count - index);
		}

		public int IndexOf(T item, int index, int count)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("The search range is invalid.");
			var eq = EqualityComparer<T>.Default;
			for (int i = 0; i < count; ++i)
				if (eq.Equals(this._data[index + i], item))
					return i;
			return -1;
		}

		/// <summary>
		/// Insert an item at the specified position. If index is equal to Count, this is in all ways identical to Add, including even being safe to use (if
		/// sufficient Capacity exists) while an element reference is taken. Any other use of Insert is not possible while an element reference is taken.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		public void Insert(int index, T item)
		{
			if (index < 0 || index > this.Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (index == this.Count)
			{
				Add(item);
				return;
			}
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot insert items except at the end while there is an active reference.");
			try
			{
				if (this.Count == this._data.Length)
				{
					var newData = new T[this.Count + 1];
					for (int i = 0; i < index; ++i)
					{
						newData[i] = this._data[i];
					}
					for (int i = index; i < this.Count; ++i)
						newData[i + 1] = this._data[i];
					newData[index] = item;
					this._data = newData;
				}
				else
				{
					for (int i = this.Count; i > index; --i)
						this._data[i] = this._data[i - 1];
					this._data[index] = item;
				}
			}
			finally
			{
				ExitResize();
			}
		}

		public void InsertRange(int index, IEnumerable<T> collection)
		{
			if (index < 0 || index > this.Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (index == this.Count)
			{
				AddRange(collection);
				return;
			}
			var incoming = collection.ToArray();
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot insert items except at the end while there is an active reference.");
			try
			{
				int count = incoming.Length;
				if ((this.Count + count) > this._data.Length)
				{
					var newData = new T[this.Count + count];
					for (int i = 0; i < index; ++i)
						newData[i] = this._data[i];
					for (int i = 0; i < count; ++i)
						newData[index + i] = incoming[i];
					for (int i = index; i < this.Count; ++i)
						newData[i + count] = this._data[i];
					this._data = newData;
				}
				else
				{
					for (int i = this.Count; i > index; --i)
					{
						this._data[i + (count - 1)] = this._data[i - 1];
					}
					for (int i = 0; i < count; ++i)
						this._data[index + i] = incoming[i];
				}
			}
			finally
			{
				ExitResize();
			}
		}

		public bool Remove(T item)
		{
			int ix = IndexOf(item);
			if (ix >= 0)
			{

				RemoveAt(ix);
				return true;
			}
			else
				return false;
		}

		public void RemoveAt(int index)
		{
			if (index < 0 || index >= this.Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot remove items while a reference is active.");
			try
			{
				for (int i = index; i < (this.Count - 1); ++i)
				{
					this._data[i] = this._data[i + 1];
				}
				this._data[--this.Count] = default(T);
			}
			finally
			{
				ExitResize();
			}
		}

		public void RemoveAll(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot remove items while an element is referenced.");
			try
			{
				int i = 0;
				int movedown = 0;
				while (i < this.Count)
				{
					if (predicate(this._data[i]))
					{
						++movedown;
						this._data[i] = default(T); // Blank it now so the GC can claim it and/or its objects.
					}
					else if (movedown > 0)
					{
						this._data[i - movedown] = this._data[i];
						this._data[i] = default(T);
					}
				}
			}
			finally
			{
				ExitResize();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="predicate"></param>
		public void RemoveAll(RefFunc<T, bool> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			// We take the resize lock, however, we're also acquiring references. However, we do so as part of our moving about.
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot remove items while an element is referenced.");
			try
			{
				int i = 0;
				int movedown = 0;
				while (i < this.Count)
				{
					if (predicate(ref this._data[i]))
					{
						++movedown;
						this._data[i] = default(T); // Blank it now so the GC can claim it and/or its objects.
					}
					else if (movedown > 0)
					{
						this._data[i - movedown] = this._data[i];
						this._data[i] = default(T);
					}
				}
			}
			finally
			{
				ExitResize();
			}
		}

		public void RemoveRange(int index, int count)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("Target range is invalid.");
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot remove items while a reference is active.");
			try
			{
				for (int i = index; i < (this.Count - count); ++i)
				{
					this._data[i] = this._data[i + count];
				}
				int oldused = this.Count;
				this.Count -= count;
				for (int i = this.Count; i < oldused; ++i)
					this._data[i] = default(T);
			}
			finally
			{
				ExitResize();
			}
		}

		public void Reverse()
		{
			Reverse(0, this.Count);
		}

		public void Reverse(int index, int count)
		{
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot Reverse the list while there are active element references.");
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("Target range is invalid.");
			try
			{
				Array.Reverse(this._data, index, count);
			}
			finally
			{
				ExitResize();
			}
		}

		public void Sort()
		{
			Sort(0, this.Count, null);
		}

		public void Sort(Comparison<T> comparer)
		{
			Sort(0, this.Count, Comparer<T>.Create(comparer));
		}

		public void Sort(IComparer<T> comparer)
		{
			Sort(0, this.Count, comparer);
		}

		public void Sort(int index, int count, IComparer<T> comparer)
		{
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot Reverse the list while there are active element references.");
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > this.Count)
				throw new ArgumentException("Target range is invalid.");
			if (comparer == null)
				comparer = Comparer<T>.Default;
			try
			{
				Array.Sort(this._data, index, count, comparer);
			}
			finally
			{
				ExitResize();
			}
		}

		public T[] ToArray()
		{
			return (new ArraySegment<T>(this._data, 0, this.Count)).ToArray();
		}

		public void TrimExcess()
		{
			this.Capacity = this.Count;
		}

		public bool TrueForAll(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			for (int i = 0; i < this.Count; ++i)
				if (!predicate(this._data[i]))
					return false;
			return true;
		}

		public bool TrueForAll(RefFunc<T, bool> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot acquire reference while a resize is in progress.");
			try
			{
				for (int i = 0; i < this.Count; ++i)
					if (!predicate(ref this._data[i]))
						return false;
				return true;
			}
			finally
			{
				ExitReference();
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}
	}
}
