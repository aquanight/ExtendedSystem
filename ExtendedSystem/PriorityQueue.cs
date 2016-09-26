using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	/// <summary>
	/// A priority queue is like a queue, but each element in the queue has an associated "priority" value.
	/// Between items of differing priorties, the item with the highest priority as determined by the comparer is retrieved first.
	/// Between items of the same priority, the item added first is retrieved first ("first-in, first-out").
	/// A comparer is associated with each priority queue instance which determines the priority of each item.
	/// For nullable types T, null values may be added to the queue if and only if the selected comparer can handle them.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public sealed class PriorityQueue<T> : ICollection<T>, IEnumerable<T>, IReadOnlyCollection<T>, IOrderedEnumerable<T>
	{
		private LinkedList<T> entries;

		/// <summary>
		/// Returns the comparer associated with this priority queue.
		/// </summary>
		public IComparer<T> Comparer
		{
			get;
		}

		/// <summary>
		/// Gets the number of items in the priority queue.
		/// </summary>
		public int Count
		{
			get
			{
				return entries.Count;
			}
		}

		/// <summary>
		/// Gets a value indicating if this queue is read-only. As such, it always returns false.
		/// </summary>
		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Creates a new empty priority queue with the default comparer.
		/// </summary>
		public PriorityQueue() : this(Comparer<T>.Default)
		{
		}

		/// <summary>
		/// Creates a new empty priority queue using the specified comparer.
		/// </summary>
		/// <param name="comparer"></param>
		public PriorityQueue(IComparer<T> comparer)
		{
			if (comparer == null)
				throw new ArgumentNullException("comparer");
			entries = new LinkedList<T>();
			Comparer = comparer;
		}

		/// <summary>
		/// Creates a new priority queue with the default comparer and the specified contents.
		/// The list is ordered by the comparer's specified priorities, and items of the same priority will be ordered in the same order they appear
		/// in the collection.
		/// </summary>
		/// <param name="collection"></param>
		public PriorityQueue(IEnumerable<T> collection) : this(collection, Comparer<T>.Default)
		{
		}

		/// <summary>
		/// Creates a new priority queue with the specified contents and comparer.
		/// The list is ordered by the comparer's specified priorities, and items of the same priority will be ordered in the same order they appear
		/// in the collection.
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="comparer"></param>
		public PriorityQueue(IEnumerable<T> collection, IComparer<T> comparer) : this(comparer)
		{
			if (collection == null)
				throw new ArgumentNullException("collection");
			if (comparer == null)
				throw new ArgumentNullException("comparer");
			AddRange(collection);
		}

		/// <summary>
		/// Adds an item to the priority queue. The item will be inserted according the associated comparer's priority.
		/// </summary>
		/// <param name="item"></param>
		public void Enqueue(T item)
		{
			for (var n = entries.First; n != null; n = n.Next)
			{
				if (Comparer.Compare(item, n.Value) < 0)
				{
					entries.AddBefore(n, item);
					return;
				}

			}
			entries.AddLast(item);
		}

		/// <summary>
		/// Removes and returns the item that has the oldest item among those with the highest priority.
		/// </summary>
		/// <returns></returns>
		public T Dequeue()
		{
			var n = entries.First;
			entries.RemoveFirst();
			return n.Value;
		}

		/// <summary>
		/// ICollection implementation, same as calling Enqueue.
		/// </summary>
		/// <param name="item"></param>
		void ICollection<T>.Add(T item)
		{
			Enqueue(item);
		}

		/// <summary>
		/// Adds a collection of items to the priority queue.
		/// The items are ordered by the comparer's specified priorities, and items of the same priority will be ordered in the same order they appear
		/// in the collection. These items will then be merged with the existing items.
		/// </summary>
		/// <param name="collection"></param>
		public void AddRange(IEnumerable<T> collection)
		{
			var sorted = collection.OrderBy((x) => x, this.Comparer);
			var n = entries.First;
			using (var e = sorted.GetEnumerator())
			{
				if (!e.MoveNext())
					return;
				while (true)
				{
					if (n == null)
					{
						do
						{
							entries.AddLast(e.Current);
						} while (e.MoveNext());
						break;
					}
					else if (Comparer.Compare(e.Current, n.Value) < 0)
					{
						entries.AddBefore(n, e.Current);
						if (!e.MoveNext())
							break;
					}
					else
					{
						n = n.Next;
					}
				}
			}
		}

		/// <summary>
		/// Removes all items in the priority queue, leaving it empty.
		/// </summary>
		public void Clear()
		{
			entries.Clear();
		}

		/// <summary>
		/// Returns true if the indicated item is in the priority queue, false otherwise.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(T item)
		{
			return entries.Contains(item);
		}

		/// <summary>
		/// Copies all items in the priority queue into the given array. The items are ordered first by priority from highest to lowest, then in the order
		/// in which they were added to the queue, from first to last.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			entries.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes the first copy of the specified item from the queue, regardless of where it appears.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(T item)
		{
			return entries.Remove(item);
		}

		/// <summary>
		/// Retrieves the enumerator for this priority queue. The objects are enumerated in order first by priority from highest to lowest, then in order
		/// in which they were added to the queue, from first to last.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<T> GetEnumerator()
		{
			return entries.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		private class SuborderedList : IOrderedEnumerable<T>
		{
			internal PriorityQueue<T> instance;
			internal IComparer<T>[] comparerChain;

			internal SuborderedList()
			{
			}

			private static IComparer<T> MakeComparer<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer, bool descending)
			{
				if (descending)
					return Comparer<T>.Create((a, b) => -comparer.Compare(keySelector(a), keySelector(b)));
				else
					return Comparer<T>.Create((a, b) => comparer.Compare(keySelector(a), keySelector(b)));
			}

			public IOrderedEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer, bool descending)
			{
				return new SuborderedList() { instance = this.instance, comparerChain = this.comparerChain.Append(MakeComparer(keySelector, comparer, descending)) };
			}

			public IEnumerator<T> GetEnumerator()
			{
				// Primary sorting must still be by priority.
				var n = instance.entries.First;
				while (n != null)
				{
					var n2 = n;
					List<LinkedListNode<T>> subset = new List<LinkedListNode<T>>();
					while (n2 != null && instance.Comparer.Compare(n.Value, n2.Value) == 0)
					{
						subset.Add(n2);
						n2 = n2.Next;
					}
					n = n2;
					IOrderedEnumerable<LinkedListNode<T>> sorted;
					using (var e = comparerChain.AsEnumerable().GetEnumerator())
					{
						if (!e.MoveNext())
							throw new InvalidOperationException("This should not be empty.");
						sorted = subset.OrderBy((nd) => nd.Value, e.Current);
						while (e.MoveNext())
						{
							sorted = sorted.ThenBy((nd) => nd.Value, e.Current);
						}
					}
					using (var e = sorted.GetEnumerator())
						while (e.MoveNext())
							yield return e.Current.Value;
				}
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}

		/// <summary>
		/// The priority queue's standard ordering is first by priority then by order in which the items were added. This creates an ordered enumerable
		/// which replaces the second-level ordering (the order in which items are added) with another of the caller's choosing.
		/// </summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="keySelector"></param>
		/// <param name="comparer"></param>
		/// <param name="descending"></param>
		/// <returns></returns>
		public IOrderedEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer, bool descending)
		{
			throw new NotImplementedException();
		}
	}
}
