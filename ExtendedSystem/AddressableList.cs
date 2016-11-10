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
		private T[] data;
		private int used;

		/// <summary>
		/// Used for locking between Resize and GetRef. This does not guarantee thread safety!! It is used to block resize attempts while there's
		/// an active reference, and to block getting references while a resize is happening.
		/// The only reason it's even needed is because GetRef is considered re-entrant (because it calls a user-supplied callback).
		/// Resize operations are not, but it does provide at least *some* protection from threading.
		/// </summary>
		private int state;

		/// <summary>
		/// Delegate which takes a reference to the array elements.
		/// </summary>
		/// <param name="value"></param>
		public AddressableList() : this(0)
		{
		}

		public AddressableList(int initialCapacity)
		{
			state = 0;
			used = 0;
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
			int _state = Interlocked.Decrement(ref state);
			if (_state < 0)
				return true;
			Interlocked.Increment(ref state);
			return false;
		}

		private void ExitResize()
		{
			Interlocked.Increment(ref state);
		}

		/// <summary>
		/// Attempt to enter the internal lock for getting reference.
		/// If *and only if* successful, you must ensure a call to ExitReference.
		/// </summary>
		/// <returns></returns>
		private bool TryEnterReference()
		{
			int _state = Interlocked.Increment(ref state);
			if (_state > 0)
				return true;
			Interlocked.Decrement(ref state);
			return false;
		}

		private void ExitReference()
		{
			Interlocked.Decrement(ref state);
		}

		/// <summary>
		/// Attempt to resize the data buffer. If an active reference is live, it will throw an exception.
		/// </summary>
		/// <param name="newCapacity"></param>
		private void Resize(int newCapacity)
		{
			if (newCapacity < used)
				throw new ArgumentOutOfRangeException(nameof(newCapacity));
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot modify Capacity while there is an active element reference.");
			try
			{
				T[] newData = (newCapacity == 0 ? Array.Empty<T>() : new T[newCapacity]);
				for (int i = 0; i < used; ++i)
					newData[i] = data[i];
				data = newData;
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
				if (index < 0 || index >= data.Length)
					throw new ArgumentOutOfRangeException(nameof(index));
				return data[index];
			}
			set
			{
				if (index < 0 || index >= data.Length)
					throw new ArgumentOutOfRangeException(nameof(index));
				data[index] = value;
			}
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
			if (index < 0 || index >= used)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			// Arguably we can just block the thread, since resizing is not re-entrant. Oh well.
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference while a resize in progress.");
			try
			{
				action(ref data[index]);
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
			if (index < 0 || index >= used)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (func == null)
				throw new ArgumentNullException(nameof(func));
			// Arguably we can just block the thread, since resizing is not re-entrant. Oh well.
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference while a resize in progress.");
			try
			{
				return func(ref data[index]);
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
				return data.Length;
			}
			set
			{
				Resize(value);
			}
		}

		public int Count
		{
			get
			{
				return used;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		public void Add(T item)
		{
			if (used >= data.Length)
				Resize(used + 1);
			data[used++] = item;
		}

		public void AddRange(IEnumerable<T> collection)
		{
			T[] incoming = collection.ToArray();
			if ((used + incoming.Length) > data.Length)
				Resize(used + incoming.Length);
			incoming.CopyTo<T>(this.data, used);
			used += incoming.Length;
		}

		public ReadOnlyCollection<T> AsReadOnly()
		{
			return new ReadOnlyCollection<T>(this);
		}

		public int BinarySearch(T item)
		{
			return BinarySearch(0, used, item, null);
		}

		public int BinarySearch(T item, IComparer<T> comparer)
		{
			return BinarySearch(0, used, item, comparer);
		}

		public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("Specified range is invalid");
			if (comparer == null)
				comparer = Comparer<T>.Default;
			return Array.BinarySearch(data, index, count, item, comparer);
		}

		public void Clear()
		{
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot clear the collection while there is an active reference.");
			try
			{
				for (int i = 0; i < used; ++i)
					data[i] = default(T);
				used = 0;
			}
			finally
			{
				ExitResize();
			}
		}

		public bool Contains(T item)
		{
			var eq = EqualityComparer<T>.Default;
			for (int i = 0; i < used; ++i)
				if (eq.Equals(data[i], item))
					return true;
			return false;
		}

		public AddressableList<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
		{
			if (converter == null)
				throw new ArgumentNullException(nameof(converter));
			AddressableList<TOutput> output = new AddressableList<TOutput>(Capacity);
			output.used = this.used;
			for (int i = 0; i < used; ++i)
				output.data[i] = converter(this.data[i]);
			return output;
		}

		public void CopyTo(T[] array)
		{
			CopyTo(array, 0);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			CopyTo(0, array, arrayIndex, used);
		}

		public void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("The source range is invalid.");
			if (arrayIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(arrayIndex));
			if (arrayIndex + count > array.Length)
				throw new ArgumentException("The destination range is invalid.");
			for (int i = 0; i < count; ++i)
				array[arrayIndex + i] = data[index + i];
		}

		public bool Exists(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			for (int i = 0; i < used; ++i)
				if (predicate(data[i]))
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
				for (int i = 0; i < used; ++i)
					if (predicate(ref data[i]))
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
			for (int i = 0; i < used; ++i)
				if (predicate(data[i]))
					return data[i];
			return default(T);
		}

		public AddressableList<T> FindAll(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			AddressableList<T> result = new AddressableList<T>(Capacity);
			for (int i = 0; i < used; ++i)
			{
				if (predicate(data[i]))
					result.data[result.used++] = data[i];
			}
			return result;
		}

		public int FindIndex(Predicate<T> predicate)
		{
			return FindIndex(0, used, predicate);
		}

		public int FindIndex(int index, Predicate<T> predicate)
		{
			return FindIndex(index, used - index, predicate);
		}

		public int FindIndex(int index, int count, Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("The search range is invalid.");
			for (int i = 0; i < count; ++i)
			{
				if (predicate(data[index + i]))
					return index + i;
			}
			return -1;
		}

		public int FindIndex(RefFunc<T, bool> predicate)
		{
			return FindIndex(0, used, predicate);
		}

		public int FindIndex(int index, RefFunc<T, bool> predicate)
		{
			return FindIndex(index, used - index, predicate);
		}

		public int FindIndex(int index, int count, RefFunc<T, bool> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("The search range is invalid.");
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference during a resize operation.");
			try
			{
				for (int i = 0; i < count; ++i)
				{
					if (predicate(ref data[index + i]))
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
			for (int i = used; i > 0; --i)
				if (predicate(data[i - 1]))
					return data[i - 1];
			return default(T);
		}

		public int FindLastIndex(Predicate<T> predicate)
		{
			return FindLastIndex(0, used, predicate);
		}

		public int FindLastIndex(int index, Predicate<T> predicate)
		{
			return FindLastIndex(index, used - index, predicate);
		}

		public int FindLastIndex(int index, int count, Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("The search range is invalid.");
			for (int i = count; i > 0; --i)
			{
				if (predicate(data[index + i - 1]))
					return index + i - 1;
			}
			return -1;
		}

		public int FindLastIndex(RefFunc<T, bool> predicate)
		{
			return FindLastIndex(0, used, predicate);
		}

		public int FindLastIndex(int index, RefFunc<T, bool> predicate)
		{
			return FindLastIndex(index, used - index, predicate);
		}

		public int FindLastIndex(int index, int count, RefFunc<T, bool> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("The search range is invalid.");
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference during a resize operation.");
			try
			{
				for (int i = count; i > 0; --i)
				{
					if (predicate(ref data[index + i - 1]))
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
			for (int i = 0; i < used; ++i)
				action(data[i]);
		}

		public void ForEach(RefAction<T> action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			if (!TryEnterReference())
				throw new InvalidOperationException("Cannot get reference during a resize operation.");
			try
			{
				for (int i = 0; i < used; ++i)
					action(ref data[i]);
			}
			finally
			{
				ExitReference();
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			for (int i = 0; i < used; ++i)
				yield return data[i];
		}

		public int IndexOf(T item)
		{
			return IndexOf(item, 0, used);
		}

		public int IndexOf(T item, int index)
		{
			return IndexOf(item, index, used - index);
		}

		public int IndexOf(T item, int index, int count)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("The search range is invalid.");
			var eq = EqualityComparer<T>.Default;
			for (int i = 0; i < count; ++i)
				if (eq.Equals(data[index + i], item))
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
			if (index < 0 || index > used)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (index == used)
			{
				Add(item);
				return;
			}
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot insert items except at the end while there is an active reference.");
			try
			{
				if (used == data.Length)
				{
					T[] newData = new T[used + 1];
					for (int i = 0; i < index; ++i)
					{
						newData[i] = data[i];
					}
					for (int i = index; i < used; ++i)
						newData[i + 1] = data[i];
					newData[index] = item;
					data = newData;
				}
				else
				{
					for (int i = used; i > index; --i)
						data[i] = data[i - 1];
					data[index] = item;
				}
			}
			finally
			{
				ExitResize();
			}
		}

		public void InsertRange(int index, IEnumerable<T> collection)
		{
			if (index < 0 || index > used)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (index == used)
			{
				AddRange(collection);
				return;
			}
			T[] incoming = collection.ToArray();
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot insert items except at the end while there is an active reference.");
			try
			{
				int count = incoming.Length;
				if ((used + count) > data.Length)
				{
					T[] newData = new T[used + count];
					for (int i = 0; i < index; ++i)
						newData[i] = data[i];
					for (int i = 0; i < count; ++i)
						newData[index + i] = incoming[i];
					for (int i = index; i < used; ++i)
						newData[i + count] = data[i];
					data = newData;
				}
				else
				{
					for (int i = used; i > index; --i)
					{
						data[i + (count - 1)] = data[i - 1];
					}
					for (int i = 0; i < count; ++i)
						data[index + i] = incoming[i];
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
			if (index < 0 || index >= used)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot remove items while a reference is active.");
			try
			{
				for (int i = index; i < (used - 1); ++i)
				{
					data[i] = data[i + 1];
				}
				data[--used] = default(T);
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
				while (i < used)
				{
					if (predicate(data[i]))
					{
						++movedown;
						data[i] = default(T); // Blank it now so the GC can claim it and/or its objects.
					}
					else if (movedown > 0)
					{
						data[i - movedown] = data[i];
						data[i] = default(T);
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
				while (i < used)
				{
					if (predicate(ref data[i]))
					{
						++movedown;
						data[i] = default(T); // Blank it now so the GC can claim it and/or its objects.
					}
					else if (movedown > 0)
					{
						data[i - movedown] = data[i];
						data[i] = default(T);
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
			if ((index + count) > used)
				throw new ArgumentException("Target range is invalid.");
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot remove items while a reference is active.");
			try
			{
				for (int i = index; i < (used - count); ++i)
				{
					data[i] = data[i + count];
				}
				int oldused = used;
				used -= count;
				for (int i = used; i < oldused; ++i)
					data[i] = default(T);
			}
			finally
			{
				ExitResize();
			}
		}

		public void Reverse()
		{
			Reverse(0, used);
		}

		public void Reverse(int index, int count)
		{
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot Reverse the list while there are active element references.");
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("Target range is invalid.");
			try
			{
				Array.Reverse(data, index, count);
			}
			finally
			{
				ExitResize();
			}
		}

		public void Sort()
		{
			Sort(0, used, null);
		}

		public void Sort(Comparison<T> comparer)
		{
			Sort(0, used, Comparer<T>.Create(comparer));
		}

		public void Sort(IComparer<T> comparer)
		{
			Sort(0, used, comparer);
		}

		public void Sort(int index, int count, IComparer<T> comparer)
		{
			if (!TryEnterResize())
				throw new InvalidOperationException("Cannot Reverse the list while there are active element references.");
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if ((index + count) > used)
				throw new ArgumentException("Target range is invalid.");
			if (comparer == null)
				comparer = Comparer<T>.Default;
			try
			{
				Array.Sort(data, index, count, comparer);
			}
			finally
			{
				ExitResize();
			}
		}

		public T[] ToArray()
		{
			return (new ArraySegment<T>(data, 0, used)).ToArray();
		}

		public void TrimExcess()
		{
			Capacity = used;
		}

		public bool TrueForAll(Predicate<T> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			for (int i = 0; i < used; ++i)
				if (!predicate(data[i]))
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
				for (int i = 0; i < used; ++i)
					if (!predicate(ref data[i]))
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
