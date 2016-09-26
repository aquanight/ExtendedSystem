using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedSystem
{
	public static class MoreEnumerable
	{
		internal class EnumerableCollection<T> : ICollection<T>
		{
			internal IEnumerable<T> wrapped;

			public int Count
			{
				get
				{
					return wrapped.Count();
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return true;
				}
			}

			public void Add(T item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public void Clear()
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			public bool Contains(T item)
			{
				return wrapped.Any((o) => o.Equals(item));
			}

			public void CopyTo(T[] array, int arrayIndex)
			{
				if (array == null)
					throw new ArgumentNullException("array");
				foreach (T o in wrapped)
				{
					if (arrayIndex < array.Length)
						array[arrayIndex++] = o;
					else
						throw new InvalidOperationException("Not enough space in array");
				}
			}

			public IEnumerator<T> GetEnumerator()
			{
				return wrapped.GetEnumerator();
			}

			public bool Remove(T item)
			{
				throw new NotSupportedException("This collection is read-only.");
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return wrapped.GetEnumerator();
			}
		}

		/// <summary>
		/// Represent an enumerable as an ICollection.
		/// If the runtime type of enumerable implements ICollection&lt;T&gt;, then enumerable is returned as-is except for being cast to ICollection&lt;T&gt;.
		/// Otherwise, a wrapper object provides ICollection implementation in terms of IEnumerable:
		/// * IsReadOnly property returns true.
		/// * As such, Add, Clear, and Remove throw NotSupportedException.
		/// * Count will iterate the collection and return the number of items resulting (using System.Linq.Enumerable.Count).
		/// * Contains will search the enumerable and return true if the item is found else false (using System.Linq.Enumerable.Any and T.Equals).
		/// * CopyTo will copy the enumerable to an array. If the array isn't big enough, it receives the prefix of the enumerable, but an exception is still thrown.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerable"></param>
		/// <returns></returns>
		public static ICollection<T> AsCollection<T>(this IEnumerable<T> enumerable)
		{
			return (enumerable as ICollection<T>) ?? new EnumerableCollection<T> { wrapped = enumerable };
		}

		public static void CopyTo<T>(this IEnumerable<T> enumerable, T[] array, int arrayIndex)
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			if (array == null)
				throw new ArgumentNullException("array");
			foreach (T o in enumerable)
			{
				if (arrayIndex < array.Length)
					array[arrayIndex++] = o;
				else
					throw new InvalidOperationException("Not enough space in array");
			}
		}

		/// <summary>
		/// Fills an array with a specific value. Like Clear, but you can specify what it fills with instead of the default value.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array"></param>
		/// <param name="value"></param>
		public static void Fill<T>(this T[] array, T value)
		{
			Fill(array, value, 0);
		}

		/// <summary>
		/// Fills an array with a specific value, starting from a given index.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array"></param>
		/// <param name="value"></param>
		/// <param name="startIndex"></param>
		public static void Fill<T>(this T[] array, T value, int startIndex)
		{
			if (array == null)
				throw new ArgumentNullException("array");
			Fill(array, value, 0, array.Length - startIndex);
		}

		/// <summary>
		/// Fills a subset of an array with a specific value, starting at the given value and filling the specified number of items.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array"></param>
		/// <param name="value"></param>
		/// <param name="startIndex"></param>
		/// <param name="count"></param>
		public static void Fill<T>(this T[] array, T value, int startIndex, int count)
		{
			if (array == null)
				throw new ArgumentNullException("array");
			if (startIndex < 0)
				throw new ArgumentOutOfRangeException("startIndex");
			if (count > array.Length - startIndex)
				throw new ArgumentOutOfRangeException("count");
			while (count-- > 0)
				array[startIndex++] = value;
		}

		/// <summary>
		/// Returns the first element of the enumerable, or if it's empty, returns null.
		/// This is only defined where T is a value type that is not Nullable&lt;T&gt;. For Nullable and for class types, use the standard FirstOrDefault() function.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerable"></param>
		/// <returns></returns>
		public static T? FirstOrNull<T>(this IEnumerable<T> enumerable) where T : struct
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			var e = enumerable.GetEnumerator();
			if (e.MoveNext())
				return e.Current;
			else
				return null;
		}

		/// <summary>
		/// Returns the first element of the enumerable, or if it's empty, returns null.
		/// This is only defined for value types that are not Nullable&lt;T&gt;. For Nullable and for class types, use the standard FirstOrDefault() function.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerable"></param>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public static T? FirstOrNull<T>(this IEnumerable<T> enumerable, Predicate<T> predicate) where T : struct
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			if (predicate == null)
				throw new ArgumentNullException("predicate");
			var e = enumerable.GetEnumerator();
			while (e.MoveNext())
			{
				T val = e.Current;
				if (predicate(val))
					return val;
			}
			return null;
		}

		/// <summary>
		/// Returns true if the enumerable is empty.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerable"></param>
		/// <returns></returns>
		public static bool None<T>(this IEnumerable<T> enumerable)
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			using (var e = enumerable.GetEnumerator())
				return !e.MoveNext();
		}

		/// <summary>
		/// Returns true if no elements of the enumerable matches the predicate (inverse of Any).
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerable"></param>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public static bool None<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			using (var e = enumerable.GetEnumerator())
				while (e.MoveNext())
					if (predicate(e.Current))
						return false;
			return true;
		}

		/// <summary>
		/// Returns the enumerable unmodified if any element matches the criteria. If no elements match, throws an exception constructed by the caller.
		/// </summary>
		/// <typeparam name="TElement"></typeparam>
		/// <typeparam name="TException"></typeparam>
		/// <param name="enumerable"></param>
		/// <param name="predicate"></param>
		/// <param name="exception"></param>
		/// <returns></returns>
		public static IEnumerable<TElement> AnyOrThrow<TElement, TException>(this IEnumerable<TElement> enumerable, Func<TElement, bool> predicate, Lazy<TException> exception) where TException : Exception
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			if (exception == null)
				throw new ArgumentNullException("exception");
			using (var e = enumerable.GetEnumerator())
				while (e.MoveNext())
					if (predicate(e.Current))
						return enumerable;
			throw exception.Value;
		}

		/// <summary>
		/// Returns the enumerable unmodified if all elements match the criteria. If any element does not match, throws an exception constructed by the caller.
		/// </summary>
		/// <typeparam name="TElement"></typeparam>
		/// <typeparam name="TException"></typeparam>
		/// <param name="enumerable"></param>
		/// <param name="predicate"></param>
		/// <param name="exception"></param>
		/// <returns></returns>
		public static IEnumerable<TElement> AllOrThrow<TElement, TException>(this IEnumerable<TElement> enumerable, Func<TElement, bool> predicate, Lazy<TException> exception) where TException : Exception
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			if (predicate == null)
				throw new ArgumentNullException("predicate");
			if (exception == null)
				throw new ArgumentNullException("exception");
			using (var e = enumerable.GetEnumerator())
				while (e.MoveNext())
					if (!predicate(e.Current))
						throw exception.Value;
			return enumerable;
		}

		/// <summary>
		/// Returns the enumerable unmodified if no elements match the criteria. If any element does match, throws an exception constructed by the caller.
		/// </summary>
		/// <typeparam name="TElement"></typeparam>
		/// <typeparam name="TException"></typeparam>
		/// <param name="enumerable"></param>
		/// <param name="predicate"></param>
		/// <param name="exception"></param>
		/// <returns></returns>
		public static IEnumerable<TElement> NoneOrThrow<TElement, TException>(this IEnumerable<TElement> enumerable, Func<TElement, bool> predicate, Lazy<TException> exception) where TException : Exception
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			if (predicate == null)
				throw new ArgumentNullException("predicate");
			if (exception == null)
				throw new ArgumentNullException("exception");
			using (var e = enumerable.GetEnumerator())
				while (e.MoveNext())
					if (predicate(e.Current))
						throw exception.Value;
			return enumerable;
		}

		/// <summary>
		/// Formats all elements in an enumerable according to a specified formatting sequence, then concatenates the resulting strings using the given delimiter.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerable"></param>
		/// <param name="format"></param>
		/// <param name="delimiter"></param>
		/// <returns></returns>
		/// <example>
		/// (new double[] { 1, 2, 3, 4 }).FormatAndConcat("{0:N1}", ", ") == "1.0, 2.0, 3.0, 4.0";
		/// </example>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.String.Format(System.String,System.Object)")]
		public static string FormatAndJoin<T>(this IEnumerable<T> enumerable, string format, string delimiter)
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			return enumerable.Select((o) => String.Format(format, o)).Aggregate((s1, s2) => s1 + delimiter + s2);
		}

		public static string FormatAndJoin<T>(this IEnumerable<T> enumerable, IFormatProvider provider, string format, string delimiter)
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");
			return enumerable.Select((o) => String.Format(provider, format, o)).Aggregate((s1, s2) => s1 + delimiter + s2);
		}

		/// <summary>
		/// Given a list and a hash code, returns an element selected based on the hash code. The selection takes the modulus of the hashCode with respect
		/// to the array length.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list"></param>
		/// <param name="hashCode"></param>
		/// <returns></returns>
		public static T GetFromHash<T>(this IList<T> list, int hashCode)
		{
			if (list == null)
				throw new ArgumentNullException("array");
			int ix = hashCode % list.Count;
			if (ix < 0)
				ix += list.Count;
			return list[ix];
		}

		/// <summary>
		/// Given a list and a hash code, assigns an element selected based on the hash code.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list"></param>
		/// <param name="hashCode"></param>
		/// <param name="value"></param>
		public static void SetFromHash<T>(this IList<T> list, int hashCode, T value)
		{
			if (list == null)
				throw new ArgumentNullException("array");
			int ix = hashCode % list.Count;
			if (ix < 0)
				ix += list.Count;
			list[ix] = value;
		}

		public static T[] Append<T>(this T[] array, T item)
		{
			T[] newary = new T[array.Length + 1];
			array.CopyTo(newary, 0);
			newary[array.Length] = item;
			return newary;
		}

		public static T[] Append<T>(this T[] array, ICollection<T> items)
		{
			T[] newary = new T[array.Length + items.Count];
			array.CopyTo(newary, 0);
			items.CopyTo(newary, array.Length);
			return newary;
		}
	}
}
