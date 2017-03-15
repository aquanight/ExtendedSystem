using System;
using System.Collections;
using System.Collections.Generic;

namespace ExtendedSystem
{
	// Various classes that provide mutating enumerators for the various standard containers.
	// These enumerators function like the normal IEnumerator<T> methods, but they include mutating members
	// such as InsertAfter, Remove, or writable Current property that modifies the container being enumerated.
	// These enuemrators *do not* detect concurrent modification of the container and may be unsafe to use in concert with the same.

	/// <summary>
	/// This class enumerates any container that implements IList&lt;T&gt;. It exposes methods to modify the current item, insert a new item before or after
	/// the current position or anywhere in the list relative to the current position, move the enumerator backward as well as forward (a bidirectional enumerator),
	/// move multiple elements at a time or access a distant element relative to the current position (random-access enumeration).
	/// 
	/// Like all IEnumerator&lt;T&gt;, this enumerator implements IDisposable. When the enumerator is disposed, all fields are invalidated and the enumerator can
	/// no longer be used: attempting to do so yields an ObjectDisposedException.
	/// 
	/// This enumerator assumes the underlying list is a 0-based list; that is, the range of valid indices to the list is the half-open interval [0 .. list.Count).
	/// 
	/// Since the enumerator itself implements IList&lt;T&gt;, it's feasible that a second enumerator over the first could be created. The behavior of such a
	/// combination is unspecified.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	public sealed class ListMutatingEnumerator<T> : IEnumerator<T>, IList<T>
	{
		private IList<T> _target;
		private int _position;
		private bool _inbetween;
		/// <summary>
		/// Retrieve the list being enumerated by this enumerator.
		/// </summary>
		public IList<T> Target
		{
			get
			{
				if (this._target == null)
					throw new ObjectDisposedException("enumerator");
				return this._target;
			}
		}

		/// <summary>
		/// Returns the current position in the list. If the enumerator is "before" the first item (as it is at initialization), this returns 0.
		/// If it is "after" the end (as it does after reaching the end of the list through standard forward enumeration), this returns Target.Count.
		/// </summary>
		public int Position
		{
			get
			{
				if (this._target == null)
					throw new ObjectDisposedException("enumerator");
				return this._position;
			}
		}
		/// <summary>
		/// Returns true if the current position is actually "before" the item indicated by Position (but still "after" the item actually previous to the
		/// same item). That is, the enumerator is "between" positions. This happens at initialization (when the enumerator is "before" the first item), after
		/// a RemoveCurrent or Reset, a RemoveAt directed at the current item, or if any Move* function returned false. If it is true, the Current property is
		/// invalid and cannot be retrieved or set, and a MoveNext, MovePrevious, or MoveBy, or MoveTo must be invoked in order to gain a valid position.
		/// </summary>
		public bool InBetweenItems
		{
			get
			{
				if (this._target == null)
					throw new ObjectDisposedException("enumerator");
				return this._inbetween;
			}
		}

		public ListMutatingEnumerator(IList<T> target)
		{
			this._target = target;
		}

		/// <summary>
		/// Retrieves or changes the item at the current enumerator's position.
		/// </summary>
		public T Current
		{
			get
			{
				if (this._inbetween)
					throw new InvalidOperationException("The current position is not valid.");
				if (this._position >= this._target.Count)
					throw new InvalidOperationException("The enumerator has passed the collection end.");
				return this.Target[this._position];
			}
			set
			{
				if (this._inbetween)
					throw new InvalidOperationException("The current position is not valid.");
				if (this._position >= this._target.Count)
					throw new InvalidOperationException("The enumerator has passed the collection end.");
				this.Target[this._position] = value;
			}
		}

		object IEnumerator.Current
		{
			get
			{
				return this.Current;
			}
		}

		/// <summary>
		/// Returns the number of items in the list at and ahead of the current position. *NOT THE TOTAL NUMBER OF ITEMS IN THE LIST.*
		/// </summary>
		public int Count
		{
			get
			{
				return this.Target.Count - this._position;
			}
		}

		/// <summary>
		/// True if the enumerated list is read-only and false otherwise.
		/// </summary>
		public bool IsReadOnly
		{
			get
			{
				return this.Target.IsReadOnly;
			}
		}

		/// <summary>
		/// Provides random-access to the target list: the list is indexed relative to the current position.
		/// If the current position is "inbetween", negative indices work normally, positive ones have 1 subtracted, and zero is the same as trying to access
		/// Current (i.e. it fails).
		/// If the current position is "past the end", then you can still retrieve items using negative indices, but not nonnegative ones.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public T this[int index]
		{
			get
			{
				if (this._inbetween && index == 0)
					throw new InvalidOperationException("The current position is not valid.");
				if (this._inbetween && index > 0)
					--index;
				index += this._position;
				if (index < 0 || index >= this.Target.Count)
					throw new InvalidOperationException("The index is outside the container's bounds.");
				return this._target[index];
			}
			set
			{
				if (this._inbetween && index == 0)
					throw new InvalidOperationException("The current position is not valid.");
				if (this._inbetween && index > 0)
					--index;
				index += this._position;
				if (index < 0 || index >= this.Target.Count)
					throw new InvalidOperationException("The index is outside the container's bounds.");
				this._target[index] = value;
			}
		}

		public void Dispose()
		{
			this._target = null;
		}

		/// <summary>
		/// Advances to the next position. If there are no more items, false is returned. At that point, Position is equal to Target.Count and InBetweenItems is
		/// true.
		/// </summary>
		/// <returns></returns>
		public bool MoveNext()
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			if (this._inbetween)
			{
				return !(this._inbetween = this._position >= this._target.Count);
			}
			++this._position;
			return !(this._inbetween = (++this._position >= this._target.Count));
		}

		public bool MovePrevious()
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			if (this._position > 0)
			{
				this._inbetween = false;
				--this._position;
				return true;
			}
			else
			{
				this._inbetween = true;
				return false;
			}
		}

		public bool MoveBy(int offset)
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			if (this._inbetween && offset == 0)
				throw new InvalidOperationException("The current position is not valid.");
			else if (this._inbetween && offset > 0)
				--offset;
			this._position += offset;
			this._inbetween = false;
			if (this._position < 0)
			{
				this._position = 0;
				this._inbetween = true;
				return false;
			}
			else if (this._position > this.Target.Count)
			{
				this._position = this.Target.Count;
				this._inbetween = true;
				return false;
			}
			return true;
		}

		public bool MoveTo(int newPosition)
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			if (newPosition < 0)
			{
				this._position = 0;
				this._inbetween = true;
				return false;
			}
			else if (newPosition > this._target.Count)
			{
				this._position = this._target.Count;
				this._inbetween = true;
				return false;
			}
			this._position = newPosition;
			this._inbetween = false;
			return true;
		}

		/// <summary>
		/// Resets the position of the enumerator back to "before the beginning".
		/// </summary>
		public void Reset()
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			this._position = 0;
			this._inbetween = true;
		}

		/// <summary>
		/// Returns the position of the specified item relative to the current position.
		/// It searches the list from the beginning and returns the first such match. Unlike standard implementations of IndexOf, this can return a negative
		/// index if the found item is before the current position. If the item is not found, the value is -(Position + 1).
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public int IndexOf(T item)
		{
			int index = this.Target.IndexOf(item);
			if (this._inbetween && index >= this._position)
				return (index + 1) - this._position;
			else
				return index - this._position;
		}

		/// <summary>
		/// Inserts a new item at the specified index relative to the current position. If an item is inserted before the current position, the position
		/// is moved so that it continues to point at the current item.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		public void Insert(int index, T item)
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			if (this._inbetween && index == 0)
				throw new InvalidOperationException("The current position is not valid.");
			else if (this._inbetween && index > 0)
				--index;
			index += this._position;
			this._target.Insert(index, item);
			if (this._position >= index)
				++this._position;
		}

		/// <summary>
		/// Insert an item after the current position.
		/// </summary>
		/// <param name="item"></param>
		public void InsertAfter(T item)
		{
			Insert(1, item);
		}

		/// <summary>
		/// Insert an item at the current position. The item so inserted becomes the current item.
		/// </summary>
		/// <param name="item"></param>
		public void InsertCurrent(T item)
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			this._target.Insert(this._position, item);
			this._inbetween = false;
		}

		/// <summary>
		/// Removes the item at the specified index. If an item is removed from before the current position, the position moves to continue pointing at the
		/// current item. If the current position is removed (index is zero), the position becomes "inbetween" the previous item and the item now at the next
		/// position: a MoveNext() must be called to advance to the item after the item just removed.
		/// </summary>
		/// <param name="index"></param>
		public void RemoveAt(int index)
		{
			if (this._target == null)
				throw new ObjectDisposedException("enumerator");
			if (this._inbetween && index == 0)
				throw new InvalidOperationException("The current position is not valid.");
			else if (this._inbetween && index > 0)
				--index;
			index += this._position;
			this._target.RemoveAt(index);
			if (this._position > index)
				--this._position;
			else if (this._position == index)
				this._inbetween = true;
		}

		/// <summary>
		/// Removes the current item.
		/// </summary>
		public void RemoveCurrent()
		{
			RemoveAt(0);
		}

		/// <summary>
		/// Adds an item to the end of the list. If the enumerator had already struck the end of the list, then a MoveNext must be called to access it.
		/// </summary>
		/// <param name="item"></param>
		public void Add(T item)
		{
			this.Target.Add(item);
		}

		/// <summary>
		/// Removes all items from the list. The enumerator is also Reset.
		/// </summary>
		public void Clear()
		{
			this.Target.Clear();
			Reset();
		}

		/// <summary>
		/// True if the list contains the item.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(T item)
		{
			return this.Target.Contains(item);
		}

		/// <summary>
		/// Copies the contents of the list to the target array.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			this.Target.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes the specified item from the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(T item)
		{
			int offset = IndexOf(item);
			if (offset >= -(this._position + 1))
			{
				RemoveAt(offset);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Simply returns the enumerator as-is.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<T> GetEnumerator()
		{
			return this;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this;
		}
	}
}