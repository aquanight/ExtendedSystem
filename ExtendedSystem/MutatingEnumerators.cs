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
				if (_target == null)
					throw new ObjectDisposedException("enumerator");
				return _target;
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
				if (_target == null)
					throw new ObjectDisposedException("enumerator");
				return _position;
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
				if (_target == null)
					throw new ObjectDisposedException("enumerator");
				return _inbetween;
			}
		}

		public ListMutatingEnumerator(IList<T> target)
		{
			_target = target;
		}

		/// <summary>
		/// Retrieves or changes the item at the current enumerator's position.
		/// </summary>
		public T Current
		{
			get
			{
				if (_inbetween)
					throw new InvalidOperationException("The current position is not valid.");
				if (_position >= _target.Count)
					throw new InvalidOperationException("The enumerator has passed the collection end.");
				return Target[_position];
			}
			set
			{
				if (_inbetween)
					throw new InvalidOperationException("The current position is not valid.");
				if (_position >= _target.Count)
					throw new InvalidOperationException("The enumerator has passed the collection end.");
				Target[_position] = value;
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
				return Target.Count - _position;
			}
		}

		/// <summary>
		/// True if the enumerated list is read-only and false otherwise.
		/// </summary>
		public bool IsReadOnly
		{
			get
			{
				return Target.IsReadOnly;
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
				if (_inbetween && index == 0)
					throw new InvalidOperationException("The current position is not valid.");
				if (_inbetween && index > 0)
					--index;
				index += _position;
				if (index < 0 || index >= Target.Count)
					throw new InvalidOperationException("The index is outside the container's bounds.");
				return _target[index];
			}
			set
			{
				if (_inbetween && index == 0)
					throw new InvalidOperationException("The current position is not valid.");
				if (_inbetween && index > 0)
					--index;
				index += _position;
				if (index < 0 || index >= Target.Count)
					throw new InvalidOperationException("The index is outside the container's bounds.");
				_target[index] = value;
			}
		}

		public void Dispose()
		{
			_target = null;
		}

		/// <summary>
		/// Advances to the next position. If there are no more items, false is returned. At that point, Position is equal to Target.Count and InBetweenItems is
		/// true.
		/// </summary>
		/// <returns></returns>
		public bool MoveNext()
		{
			if (_target == null)
				throw new ObjectDisposedException("enumerator");
			if (_inbetween)
			{
				return !(_inbetween = _position >= _target.Count);
			}
			++_position;
			return !(_inbetween = (++_position >= _target.Count));
		}

		public bool MovePrevious()
		{
			if (_target == null)
				throw new ObjectDisposedException("enumerator");
			if (_position > 0)
			{
				_inbetween = false;
				--_position;
				return true;
			}
			else
			{
				_inbetween = true;
				return false;
			}
		}

		public bool MoveBy(int offset)
		{
			if (_target == null)
				throw new ObjectDisposedException("enumerator");
			if (_inbetween && offset == 0)
				throw new InvalidOperationException("The current position is not valid.");
			else if (_inbetween && offset > 0)
				--offset;
			_position += offset;
			_inbetween = false;
			if (_position < 0)
			{
				_position = 0;
				_inbetween = true;
				return false;
			}
			else if (_position > Target.Count)
			{
				_position = Target.Count;
				_inbetween = true;
				return false;
			}
			return true;
		}

		public bool MoveTo(int newPosition)
		{
			if (_target == null)
				throw new ObjectDisposedException("enumerator");
			if (newPosition < 0)
			{
				_position = 0;
				_inbetween = true;
				return false;
			}
			else if (newPosition > _target.Count)
			{
				_position = _target.Count;
				_inbetween = true;
				return false;
			}
			_position = newPosition;
			_inbetween = false;
			return true;
		}

		/// <summary>
		/// Resets the position of the enumerator back to "before the beginning".
		/// </summary>
		public void Reset()
		{
			if (_target == null)
				throw new ObjectDisposedException("enumerator");
			_position = 0;
			_inbetween = true;
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
			int index = Target.IndexOf(item);
			if (_inbetween && index >= _position)
				return (index + 1) - _position;
			else
				return index - _position;
		}

		/// <summary>
		/// Inserts a new item at the specified index relative to the current position. If an item is inserted before the current position, the position
		/// is moved so that it continues to point at the current item.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		public void Insert(int index, T item)
		{
			if (_target == null)
				throw new ObjectDisposedException("enumerator");
			if (_inbetween && index == 0)
				throw new InvalidOperationException("The current position is not valid.");
			else if (_inbetween && index > 0)
				--index;
			index += _position;
			_target.Insert(index, item);
			if (_position >= index)
				++_position;
		}

		/// <summary>
		/// Removes the item at the specified index. If an item is removed from before the current position, the position moves to continue pointing at the
		/// current item. If the current position is removed (index is zero), the position becomes "inbetween" the previous item and the item now at the next
		/// position: a MoveNext() must be called to advance to the item after the item just removed.
		/// </summary>
		/// <param name="index"></param>
		public void RemoveAt(int index)
		{
			if (_target == null)
				throw new ObjectDisposedException("enumerator");
			if (_inbetween && index == 0)
				throw new InvalidOperationException("The current position is not valid.");
			else if (_inbetween && index > 0)
				--index;
			index += _position;
			_target.RemoveAt(index);
			if (_position > index)
				--_position;
			else if (_position == index)
				_inbetween = true;
		}

		/// <summary>
		/// Adds an item to the end of the list. If the enumerator had already struck the end of the list, then a MoveNext must be called to access it.
		/// </summary>
		/// <param name="item"></param>
		public void Add(T item)
		{
			Target.Add(item);
		}

		/// <summary>
		/// Removes all items from the list. The enumerator is also Reset.
		/// </summary>
		public void Clear()
		{
			Target.Clear();
			Reset();
		}

		/// <summary>
		/// True if the list contains the item.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(T item)
		{
			return Target.Contains(item);
		}

		/// <summary>
		/// Copies the contents of the list to the target array.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			Target.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes the specified item from the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(T item)
		{
			int offset = IndexOf(item);
			if (offset >= -(_position + 1))
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