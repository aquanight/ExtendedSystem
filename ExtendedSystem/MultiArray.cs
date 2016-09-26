using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	/// <summary>
	/// Provides various methods to improve support of multi-dimensional arrays, but not non-zero based arrays.
	/// Type-safe and more efficient overloads of each method are provided for 2-dimensional and 3-dimensional arrays.
	/// Anything that enumerates through an array or portion of an array does so in "row major" order. For example:
	/// For a 2-dimensional array of 4 by 6, the 6 elements of the first row are searched, then the 6 of the second, and so on to the fourth.
	/// For a 3-dimensional array of 2 by 8 by 3, there are two rows of 8 "subrows" of 3 items.
	/// </summary>
	public static class MultiArray
	{
		/// <summary>
		/// Checks that the specified positions are within the array's specified bounds.
		/// Throws an exception if it is not.
		/// Throws an exception if either is null, or if the position is not correct length for the array's rank.
		/// </summary>
		/// <param name="array">The array to check</param>
		/// <param name="index">The position to check</param>
		/// <returns></returns>
		public static void CheckBounds(Array array, int[] index)
		{
			if (array == null)
				throw new ArgumentNullException("array");
			if (index == null)
				throw new ArgumentNullException("position");
			if (array.Rank != index.Length)
				throw new RankException();
			for (int i = 0; i < index.Length; ++i)
			{
				if (index[i] < array.GetLowerBound(i))
					throw new IndexOutOfRangeException();
				if (index[i] > array.GetUpperBound(i))
					throw new IndexOutOfRangeException();
			}
		}

		public static void CheckBounds<T>(T[,] array, int row, int column)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (row < array.GetLowerBound(0) || row > array.GetUpperBound(0))
				throw new IndexOutOfRangeException();
			if (column < array.GetLowerBound(1) || column > array.GetUpperBound(1))
				throw new IndexOutOfRangeException();
		}

		public static void CheckBounds<T>(T[,,] array, int x, int y, int z)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (x < array.GetLowerBound(0) || x > array.GetUpperBound(0))
				throw new IndexOutOfRangeException();
			if (y < array.GetLowerBound(1) || y > array.GetUpperBound(1))
				throw new IndexOutOfRangeException();
			if (z < array.GetLowerBound(2) || z > array.GetUpperBound(2))
				throw new IndexOutOfRangeException();
		}

		/// <summary>
		/// Checks that the specified region of an array is entirely within its bounds.
		/// Throws an exception if it is not.
		/// Throws an exception if either is null or if the position or length is not the correct length for the array's rank.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		/// <param name="length"></param>
		public static void CheckBounds(Array array, int[] index, int[] length)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index == null)
				throw new ArgumentNullException(nameof(index));
			if (length == null)
				throw new ArgumentNullException(nameof(length));
			if (array.Rank != index.Length || array.Rank != length.Length)
				throw new RankException();
			for (int i = 0; i < index.Length; ++i)
			{
				if (index[i] < array.GetLowerBound(i))
					throw new IndexOutOfRangeException();
				if (index[i] > array.GetUpperBound(i))
					throw new IndexOutOfRangeException();
				if (index[i] + length[i] - 1 > array.GetUpperBound(i))
					throw new IndexOutOfRangeException();
			}
		}

		public static void CheckBounds<T>(T[,] array, int row, int column, int numRows, int numColumns)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (row < array.GetLowerBound(0) || row > array.GetUpperBound(0) || row + numRows - 1 > array.GetUpperBound(0))
				throw new IndexOutOfRangeException();
			if (column < array.GetLowerBound(1) || column > array.GetUpperBound(1) || column + numColumns - 1 > array.GetUpperBound(1))
				throw new IndexOutOfRangeException();
		}

		public static void CheckBounds<T>(T[,,] array, int x, int y, int z, int dx, int dy, int dz)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (x < array.GetLowerBound(0) || x > array.GetUpperBound(0) || x + dx - 1 > array.GetUpperBound(0))
				throw new IndexOutOfRangeException();
			if (y < array.GetLowerBound(1) || y > array.GetUpperBound(1) || y + dy - 1 > array.GetUpperBound(1))
				throw new IndexOutOfRangeException();
			if (z < array.GetLowerBound(2) || z > array.GetUpperBound(2) || z + dz - 1 > array.GetUpperBound(2))
				throw new IndexOutOfRangeException();
		}

		private static int[] PositionAdd(int[] index, int[] offset)
		{
			if (index == null)
				throw new ArgumentNullException(nameof(index));
			if (offset == null)
				throw new ArgumentNullException(nameof(offset));
			if (index.Length != offset.Length)
				throw new ArgumentException("index and offset must be same length");
			int[] result = index.Duplicate();
			for (int i = 0; i < result.Length; ++i)
				result[i] += offset[i];
			return result;
		}

		public static IEnumerable<int[]> EnumeratePositions(int[] index, int[] length)
		{
			if (index == null)
				throw new ArgumentNullException(nameof(index));
			if (length == null)
				throw new ArgumentNullException(nameof(length));
			if (index.Length != length.Length)
				throw new ArgumentException("index and length must be same length");
			int[] factors = new int[length.Length + 1];
			factors[length.Length] = 1;
			for (int i = factors.Length - 1; i >= 0; --i)
			{
				if (length[i] < 0)
					throw new IndexOutOfRangeException("negative length");
				factors[i] = factors[i + 1] * length[i];
			}
			for (int ix = 0; ix < factors[0]; ++ix)
			{
				int[] pos = index.Duplicate();
				for (int r = 0; r < pos.Length; ++r)
				{
					pos[r] += (ix % factors[r]) / factors[r + 1];
				}
				yield return pos;
			}
		}

		public static IEnumerable<int[]> EnumeratePositions(Array array)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			int[] pos = Enumerable.Range(0, array.Rank).Select((r) => array.GetLowerBound(r)).ToArray();
			int[] len = Enumerable.Range(0, array.Rank).Select((r) => array.GetLength(r)).ToArray();
			return EnumeratePositions(pos, len);
		}

		public static IEnumerable<int[]> EnumeratePositions(int row, int column, int numRows, int numColumns)
		{
			if (numRows < 0 || numColumns < 0)
				throw new IndexOutOfRangeException("negative length");
			for (int ri = 0; ri < numRows; ++ri)
				for (int ci = 0; ci < numRows; ++ci)
					yield return new int[] { row + ri, column + ci };
		}

		public static IEnumerable<int[]> EnumeratePositions(int x, int y, int z, int dx, int dy, int dz)
		{
			if (dx < 0 || dy < 0 || dz < 0)
				throw new IndexOutOfRangeException("negative length");
			for (int ix = 0; ix < dx; ++ix)
				for (int iy = 0; iy < dy; ++iy)
					for (int iz = 0; iz < dz; ++iz)
						yield return new int[] { x + ix, y + iy, z + iz };
		}

		public static System.Collections.IEnumerable EnumerateRegion(Array array, int[] index, int[] length)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index == null)
				throw new ArgumentNullException(nameof(index));
			if (length == null)
				throw new ArgumentNullException(nameof(length));
			CheckBounds(array, index, length);
			foreach (var pos in EnumeratePositions(index, length))
				yield return array.GetValue(pos);
		}

		public static IEnumerable<T> EnumerateRegion<T>(T[,] array, int row, int column, int numRows, int numColumns)
		{
			CheckBounds(array, row, column, numRows, numColumns);
			for (int ri = 0; ri < numRows; ++ri)
				for (int ci = 0; ci < numRows; ++ci)
					yield return array[row + ri, column + ci];
		}

		public static IEnumerable<T> EnumerateRegion<T>(T[,,] array, int x, int y, int z, int dx, int dy, int dz)
		{
			CheckBounds(array, x, y, z, dx, dy, dz);
			for (int ix = 0; ix < dx; ++ix)
				for (int iy = 0; iy < dx; ++iy)
					for (int iz = 0; iz < dz; ++iz)
						yield return array[x + ix, y + iy, z + dz];
		}

		/// <summary>
		/// Compute the vector index for the given array and position.
		/// This is primarily useful for supporting multi-dimensional arrays through the standard Array class members that vectorize multi-dimensional arrays.
		/// The index returned is suitable for use with functions like Array.Copy and Array.Clear.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static int GetVectorIndex(Array array, int[] index)
		{
			CheckBounds(array, index);
			int pos = 0;
			int[] factors = new int[array.Rank];
			factors[factors.Length - 1] = 1;
			for (int i = factors.Length - 1; i >= 0; --i)
			{
				factors[i] = factors[i + 1] * array.GetLength(i + 1);
			}
			for (int r = 0; r < index.Length - 1; ++r)
				pos += (index[r] - array.GetLowerBound(r)) * factors[r];
			pos += index[index.Length - 1] - array.GetLowerBound(index.Length - 1);
			return pos + array.GetLowerBound(0);
		}

		public static int GetVectorIndex<T>(T[,] array, int row, int column)
		{
			CheckBounds(array, row, column);
			return ((row - array.GetLowerBound(0)) * array.GetLength(1)) + (column - array.GetLowerBound(1)) + array.GetLowerBound(0);
		}

		public static int GetVectorIndex<T>(T[,,] array, int x, int y, int z)
		{
			CheckBounds(array, x, y, z);
			return ((x - array.GetLowerBound(0)) * array.GetLength(1) * array.GetLength(2)) + ((y - array.GetLowerBound(1)) * array.GetLength(2)) + (z - array.GetLowerBound(2)) + array.GetLowerBound(0);
		}

		/// <summary>
		/// Clear a region of a multi-dimensioned array, setting the affected elements to the default value for the array's element type.
		/// </summary>
		/// <param name="array">The array to clear.</param>
		/// <param name="index">The position at which to start.</param>
		/// <param name="length">The extent to clear.</param>
		public static void Clear(Array array, int[] index, int[] length)
		{
			CheckBounds(array, index, length);
			int[] fl = length.Duplicate();
			int rl = length[length.Length - 1];
			fl[fl.Length - 1] = 1;
			foreach (var pos in EnumeratePositions(index, fl))
			{
				int vix = GetVectorIndex(array, pos);
				Array.Clear(array, vix, rl);
			}
		}

		public static void Clear<T>(T[,] array, int row, int column, int numRows, int numColumns)
		{
			CheckBounds(array, row, column, numRows, numColumns);
			for (int r = row; r < (row + numRows); ++r)
			{
				int vix = GetVectorIndex(array, r, column);
				Array.Clear(array, vix, numColumns);
			}
		}

		public static void Clear<T>(T[,,] array, int x, int y, int z, int dx, int dy, int dz)
		{
			CheckBounds(array, x, y, z, dx, dy, dz);
			for (int ix = x; ix < (x + dx); ++ix)
				for (int iy = y; iy < (y + dy); ++iy)
				{
					int vix = GetVectorIndex(array, x, y, z);
					Array.Clear(array, vix, dz);
				}
		}

		/// <summary>
		/// Copies a region from one array to another. Guarantees that no changes are made if the copy fails.
		/// </summary>
		/// <param name="sourceArray"></param>
		/// <param name="sourceIndex"></param>
		/// <param name="destArray"></param>
		/// <param name="destIndex"></param>
		/// <param name="length"></param>
		public static void ConstrainedCopy(Array sourceArray, int[] sourceIndex, Array destArray, int[] destIndex, int[] length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException(nameof(sourceArray));
			if (destArray == null)
				throw new ArgumentNullException(nameof(destArray));
			if (sourceIndex == null)
				throw new ArgumentNullException(nameof(sourceIndex));
			if (destIndex == null)
				throw new ArgumentNullException(nameof(destIndex));
			if (length == null)
				throw new ArgumentNullException(nameof(length));
			if (sourceArray.Rank != sourceIndex.Length)
				throw new RankException();
			if (sourceIndex.Rank != length.Length)
				throw new RankException();
			if (destArray.Rank != destIndex.Length)
				throw new RankException();
			if (destArray.Rank != length.Length)
				throw new RankException();
			CheckBounds(sourceArray, sourceIndex, length);
			CheckBounds(destArray, destIndex, length);
			Array tmp = Array.CreateInstance(destArray.GetType().GetElementType(), length);
			int[] zero = new int[length.Length];
			int[] fl = length.Duplicate();
			int rl = length[length.Length - 1];
			fl[fl.Length - 1] = 1;
			foreach (var pos in EnumeratePositions(zero, fl))
			{
				int[] srcpos = PositionAdd(sourceIndex, pos);
				int srcvix = GetVectorIndex(sourceArray, srcpos);
				int dstvix = GetVectorIndex(tmp, pos);
				Array.Copy(sourceArray, srcvix, tmp, dstvix, rl);
			}
			// Initial copy successful, transplant to target array.
			foreach (var pos in EnumeratePositions(zero, fl))
			{
				int[] dstpos = PositionAdd(destIndex, pos);
				int srcvix = GetVectorIndex(tmp, pos);
				int dstvix = GetVectorIndex(destArray, dstpos);
				Array.Copy(tmp, srcvix, destArray, dstvix, rl);
			}
		}

		public static void ConstrainedCopy<T, U>(T[,] sourceArray, int sourceRow, int sourceColumn, U[,] destArray, int destRow, int destColumn, int numRows, int numColumns) where U : T
		{
			if (sourceArray == null)
				throw new ArgumentNullException(nameof(sourceArray));
			if (destArray == null)
				throw new ArgumentNullException(nameof(destArray));
			CheckBounds(sourceArray, sourceRow, sourceColumn, numRows, numColumns);
			CheckBounds(destArray, destRow, destColumn, numRows, numColumns);
			U[,] tmp = new U[numRows, numColumns];
			for (int ir = 0; ir < numRows; ++ir)
			{
				int srcvix = GetVectorIndex(sourceArray, sourceRow + ir, sourceColumn);
				int dstvix = GetVectorIndex(tmp, ir, 0);
				Array.Copy(sourceArray, srcvix, tmp, dstvix, numColumns);
			}
			// Initial copy successful, transplant to target array
			for (int ir = 0; ir < numRows; ++ir)
			{
				int srcvix = GetVectorIndex(tmp, ir, 0);
				int dstvix = GetVectorIndex(destArray, destRow + ir, destColumn);
				Array.Copy(tmp, srcvix, destArray, dstvix, numColumns);
			}
		}

		public static void ConstrainedCopy<T, U>(T[,,] sourceArray, int sourceX, int sourceY, int sourceZ, U[,,] destArray, int destX, int destY, int destZ, int dx, int dy, int dz) where U : T
		{
			if (sourceArray == null)
				throw new ArgumentNullException(nameof(sourceArray));
			if (destArray == null)
				throw new ArgumentNullException(nameof(destArray));
			CheckBounds(sourceArray, sourceX, sourceY, sourceZ, dx, dy, dz);
			CheckBounds(destArray, destX, destY, destZ, dx, dy, dz);
			U[,,] tmp = new U[dx, dy, dz];
			for (int ix = 0; ix < dx; ++ix)
				for (int iy = 0; iy < dy; ++iy)
				{
					int srcvix = GetVectorIndex(sourceArray, sourceX + ix, sourceY + iy, sourceY);
					int dstvix = GetVectorIndex(tmp, ix, iy, 0);
					Array.Copy(sourceArray, srcvix, tmp, dstvix, dz);
				}
			// Initial copy successful, transplant to target array
			for (int ix = 0; ix < dx; ++ix)
				for (int iy = 0; iy < dy; ++iy)
				{
					int srcvix = GetVectorIndex(tmp, ix, iy, 0);
					int dstvix = GetVectorIndex(destArray, destX + ix, destY + iy, destZ);
					Array.Copy(tmp, srcvix, destArray, dstvix, dz);
				}
		}

		/// <summary>
		/// Returns an empty array with the given rank.
		/// NOTE: if rank is 1 this creates a multidimensional array with a rank of 1, which is distinct from a standard single-dimensioned array.
		/// In particular, such an array is not assignable to T[] or IList&lt;T&gt;.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="rank"></param>
		/// <returns></returns>
		public static Array Empty<T>(int rank)
		{
			if (rank < 1)
				throw new ArgumentOutOfRangeException("rank", "rank must be postive");
			int[] length = new int[rank];
			return Array.CreateInstance(typeof(T), length);
		}

		/// <summary>
		/// Search an array for an element which meets the specified criteria. Elements which are not assignable to the supplied type parameter will be skipped.
		/// The return value is true if the element is found, else false.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array"></param>
		/// <param name="match"></param>
		/// <returns></returns>
		public static bool Exists<T>(Array array, Predicate<T> match)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			return array.OfType<T>().Any((e) => match(e));
		}

		public static bool Exists<T>(T[,] array, Predicate<T> match)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			for (int r = array.GetLowerBound(0); r <= array.GetUpperBound(0); ++r)
				for (int c = array.GetLowerBound(1); r <= array.GetUpperBound(1); ++c)
					if (match(array[r, c]))
						return true;
			return false;
		}

		public static bool Exists<T>(T[,,] array, Predicate<T> match)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			for (int x = array.GetLowerBound(1); x <= array.GetUpperBound(1); ++x)
				for (int y = array.GetLowerBound(0); x <= array.GetUpperBound(0); ++y)
					for (int z = array.GetLowerBound(2); z <= array.GetLowerBound(2); ++z)
						if (match(array[x, y, z]))
							return true;
			return false;
		}

		/// <summary>
		/// Search the array for an elementw hich meets the specified criteria and returns it. Elements which are not assignable to the supplied type parameter will
		/// be skipped.
		/// The found value is returned. If no value is found, the default value of T is returned.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array"></param>
		/// <param name="match"></param>
		/// <returns></returns>
		public static T Find<T>(Array array, Predicate<T> match)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			return array.OfType<T>().FirstOrDefault((e) => match(e));
		}

		public static T Find<T>(T[,] array, Predicate<T> match)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			for (int r = array.GetLowerBound(0); r <= array.GetUpperBound(0); ++r)
				for (int c = array.GetLowerBound(1); r <= array.GetUpperBound(1); ++c)
					if (match(array[r, c]))
						return array[r, c];
			return default(T);
		}

		public static T Find<T>(T[,,] array, Predicate<T> match)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			for (int x = array.GetLowerBound(0); x <= array.GetUpperBound(0); ++x)
				for (int y = array.GetLowerBound(1); x <= array.GetUpperBound(1); ++y)
					for (int z = array.GetLowerBound(2); z <= array.GetUpperBound(2); ++z)
						if (match(array[x, y, z]))
							return array[x, y, z];
			return default(T);
		}

		/// <summary>
		/// Converts an array of multiple dimensionals into a "vector" array - a single-dimensioned array with a lower bound of zero.
		/// The resulting array has the same element type as the input array.
		/// </summary>
		/// <param name="array"></param>
		/// <returns></returns>
		public static Array Vectorize(Array array)
		{
			if (array == null)
				return null;
			Type et = array.GetType().GetElementType();
			Array dstary = Array.CreateInstance(et, array.Length);
			int oi = 0;
			foreach (object var in array)
			{
				dstary.SetValue(var, oi++);
			}
			return dstary;
		}

		/// <summary>
		/// Splits the bottom dimension of an array at the given size level. The excess of the resulting array is default-initialized.
		/// The following postconditions will hold, assuming no exception is thrown:
		/// - The resulting array has rank equal to one plus the original array's rank.
		/// - The size of new rank 1 is equal to <paramref name="size"/>.
		/// - The size of new rank 0 is equal to the size of old rank 0 divided by new rank 1, rounded up to the nearest integer.
		/// - The size of new rank 2 and subsequent ranks is equal to the size of old rank 1 and subsequent ranks.
		/// - Indexes previously valid for old rank 1 and subsequent ranks are now valid for new rank 2 and subsequent ranks.
		/// - The lower bound of new rank 1 and subsequent ranks are equal to the lower bound of old rank 0 and subsequent ranks.
		/// - The lower bound of new rank 0 is 0.
		/// - The resulting array's element type is the same as the original arary's element type.
		/// - Given an element located at old position [i0, i1, i2, ...] with i0 equal to (r * size) + c for some r, c with c &lt; size, that element is copied to
		///   new position [r, c, i1, i2, ...].
		/// - Where the new position [r, c, i1, i2, ...] has r, c such that (r * size) + c is greater than the upper bound of old rank 0, that element holds the
		///   default value for the array's element type. (Reference types get a null reference, value types get a value created by the type's default constructor.)
		///   This situation happens when the length of old rank 0 is not evenly divided by size. In which case, the number of new positions [r, c] is equal to size
		///   minus the modulus of old length 0 against size, and the number of new such elements are this number of such new positions times the product of the
		///   lengths of old rank 1 and subsequent ranks.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="size"></param>
		/// <returns></returns>
		public unsafe static Array SplitDimension(Array array, int size)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (size <= 0)
				throw new ArgumentOutOfRangeException(nameof(size), "size must be positive");
			int osz = array.GetLength(0);
			int olb = array.GetLowerBound(0);
			int[] lbs = new int[array.Rank + 1];
			int[] lens = new int[array.Rank + 1];
			lbs[0] = 0;
			for (int r = 0; r < array.Rank; ++r)
			{
				lbs[r + 1] = array.GetLowerBound(r);
				lens[r + 1] = array.GetLength(r);
			}
			lens[0] = osz / size + (osz % size > 0 ? 1 : 0);
			lens[1] = size;
			Array result = Array.CreateInstance(array.GetType().GetElementType(), lens, lbs);
			foreach (int[] pos in EnumeratePositions(array))
			{
				int[] dstpos = new int[result.Rank];
				Array.Copy(pos, 1, dstpos, 2, result.Rank - 1);
				int oldoff = pos[0] - olb;
				dstpos[0] = oldoff / size;
				dstpos[1] = (oldoff % size) + olb;
				result.SetValue(array.GetValue(pos), dstpos);
			}
			return result;
		}

		/// <summary>
		/// Joins the bottom dimension of array, returning the resulting array.
		/// If the array is already one-dimensional then an exception is thrown.
		/// Given an input array with rank iR, lower bounds [ilb1, ilb2, ..., ilbR], and lengths [iln1, iln2, ..., ilnR]
		/// - The output array has rank R - 1.
		/// - The output array has length [iln1 * iln2, iln3, ..., ilnR]
		/// - The output array has lower bounds [ilb1 * ilb2, ilb3, ..., ilbR]
		/// - Given an index [i1, i2, ..., iR] in the input array, the following produces a valid index [newix, i3, ..., iR]:
		///   newix = ((i1 - ilb1) * iln2) + (i2 - ilb2) + (ilb1 * ilb2)
		/// </summary>
		/// <param name="array"></param>
		/// <returns></returns>
		public static Array JoinDimension(Array array)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (array.Rank < 2)
				throw new RankException();
			int[] lbs = new int[array.Rank - 1];
			int[] lns = new int[array.Rank - 1];
			for (int r = 2; r < array.Rank; ++r)
			{
				lbs[r - 1] = array.GetLowerBound(r);
				lns[r - 1] = array.GetLength(r);
			}
			int olb0 = array.GetLowerBound(0);
			int olb1 = array.GetLowerBound(1);
			int oln1 = array.GetLength(1);
			lbs[0] = array.GetLowerBound(0) * array.GetLowerBound(1);
			lns[0] = array.GetLength(0) * array.GetLength(1);
			Array result = Array.CreateInstance(array.GetType().GetElementType(), lns, lbs);
			foreach (int[] pos in EnumeratePositions(array))
			{
				int[] dstpos = new int[result.Rank];
				Array.Copy(pos, 2, dstpos, 1, dstpos.Length);
				dstpos[0] = ((pos[0] - olb0) * oln1) + (pos[1] - olb1) + lbs[0];
				result.SetValue(array.GetValue(pos), dstpos);
			}
			return result;
		}

		/// <summary>
		/// Resizes a multi-dimensional array. You may optionally change its lower bounds.
		/// The element type and rank of the new array remain the same as the original array and cannot be changed.
		/// Elements of the original array are retained at their current indexes, provided those indexes yet still exist in the resized array.
		/// If a lower bound is lowered or an upper bound is raised, the new elements get their default values.
		/// If a lower bound is raised or an upper bound lowered, the values of the removed elements are discarded.
		/// An upper bound is lowered if the lower bound is lowered but the length is not raised by an equal or greater amount, or if the length is lowered
		/// and the lower bound is not raised by an equal or greater amount.
		/// An upper bound is raised if the lower bound is raised but the length is not lowered by an equal or greater amount, or if the length is raised but the
		/// lower bound is not lowered by an equal or greater amount.
		/// If no portion of the resized array overlaps the old one, then the caller effectively will have replaced the old array with a new, blank array.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="newLengths"></param>
		/// <param name="newLowerBounds"></param>
		public static void Resize<T>(ref T array, int[] newLengths, int[] newLowerBounds) where T : class
		{
			// Constraints that can't be implemented at compile-time :(
			if (!typeof(T).IsArray)
				throw new InvalidCastException("Only array types are permitted.");
			if (newLengths == null)
				throw new ArgumentNullException(nameof(newLengths));
			if (newLowerBounds == null)
				throw new ArgumentNullException(nameof(newLowerBounds));
			if (typeof(T).GetArrayRank() != newLengths.Length || typeof(T).GetArrayRank() != newLowerBounds.Length)
				throw new RankException();
			if (newLengths.Any((l) => l < 0))
				throw new ArgumentOutOfRangeException(nameof(newLengths), "A negative length cannot be supplied");
			Array newAry = Array.CreateInstance(typeof(T).GetElementType(), newLengths, newLowerBounds);
			if (array == null)
				goto SkipCopy;
			Array ary = (Array)(object)array;
			int[] copyPos = new int[newLowerBounds.Length];
			int[] copyLen = new int[newLengths.Length];
			for (int i = 0; i < newLengths.Length; ++i)
			{
				copyPos[i] = Math.Max(ary.GetLowerBound(i), newLowerBounds[i]);
				copyLen[i] = Math.Min(ary.GetLength(i) - copyPos[i], newLengths[i]);
				if (copyLen[i] <= 0)
					goto SkipCopy;
			}
			ConstrainedCopy(ary, copyPos, newAry, copyPos, copyLen);
		SkipCopy:
			array = (T)(object)newAry;
		}

		public static void Resize<T>(ref T[,] array, int newRows, int newColumns)
		{
			if (newRows < 0)
				throw new ArgumentOutOfRangeException(nameof(newRows), "A negative length cannot be supplied");
			if (newColumns < 0)
				throw new ArgumentOutOfRangeException(nameof(newColumns), "A negative length cannot be supplied");
			int lr = array?.GetLowerBound(0) ?? 0;
			int lc = array?.GetLowerBound(1) ?? 0;
			T[,] newAry = (T[,])Array.CreateInstance(typeof(T), new int[] { newRows, newColumns }, new int[] { lr, lc });
			if (array == null)
				goto SkipCopy;
			int copyRows = Math.Min(newRows, array.GetLength(0));
			int copyCols = Math.Min(newColumns, array.GetLength(1));
			if (copyRows == 0 || copyCols == 0)
				goto SkipCopy;
			ConstrainedCopy(array, lr, lc, newAry, lr, lc, copyRows, copyCols);
		SkipCopy:
			array = newAry;
		}

		public static void Resize<T>(ref T[,,] array, int newDx, int newDy, int newDz)
		{
			if (newDx < 0)
				throw new ArgumentOutOfRangeException(nameof(newDx), "A negative length cannot be supplied");
			if (newDy < 0)
				throw new ArgumentOutOfRangeException(nameof(newDy), "A negative length cannot be supplied");
			if (newDz < 0)
				throw new ArgumentOutOfRangeException(nameof(newDz), "A negative length cannot be supplied");
			int lx = array?.GetLowerBound(0) ?? 0;
			int ly = array?.GetLowerBound(1) ?? 0;
			int lz = array?.GetLowerBound(2) ?? 0;
			T[,,] newAry = (T[,,])Array.CreateInstance(typeof(T), new int[] { newDx, newDy, newDz }, new int[] { lx, ly, lz });
			if (array == null)
				goto SkipCopy;
			int copyX = Math.Min(newDx, array.GetLength(0));
			int copyY = Math.Min(newDy, array.GetLength(1));
			int copyZ = Math.Min(newDz, array.GetLength(2));
			if (copyX == 0 || copyY == 0 || copyZ == 0)
				goto SkipCopy;
			ConstrainedCopy(array, lx, ly, lz, newAry, lx, ly, lz, copyX, copyY, copyZ);
		SkipCopy:
			array = newAry;
		}

		public static void Resize<T>(ref T[,] array, int newRows, int newColumns, int newStartRow, int newStartColumn)
		{
			if (newRows < 0)
				throw new ArgumentOutOfRangeException(nameof(newRows), "A negative length cannot be supplied");
			if (newColumns < 0)
				throw new ArgumentOutOfRangeException(nameof(newColumns), "A negative length cannot be supplied");
			T[,] newAry = (T[,])Array.CreateInstance(typeof(T), new int[] { newRows, newColumns }, new int[] { newStartRow, newStartColumn });
			if (array == null)
				goto SkipCopy;
			int lr = Math.Max(array.GetLowerBound(0), newStartRow);
			int lc = Math.Max(array.GetLowerBound(1), newStartColumn);
			int copyRows = Math.Min(newRows, array.GetUpperBound(0) - lr);
			int copyCols = Math.Min(newColumns, array.GetUpperBound(1) - lc);
			if (copyRows <= 0 || copyCols <= 0)
				goto SkipCopy;
			ConstrainedCopy(array, lr, lc, newAry, lr, lc, copyRows, copyCols);
		SkipCopy:
			array = newAry;
		}

		public static void Resize<T>(ref T[,,] array, int newDx, int newDy, int newDz, int newStartX, int newStartY, int newStartZ)
		{
			if (newDx < 0)
				throw new ArgumentOutOfRangeException(nameof(newDx), "A negative length cannot be supplied");
			if (newDy < 0)
				throw new ArgumentOutOfRangeException(nameof(newDy), "A negative length cannot be supplied");
			if (newDz < 0)
				throw new ArgumentOutOfRangeException(nameof(newDz), "A negative length cannot be supplied");
			T[,,] newAry = (T[,,])Array.CreateInstance(typeof(T), new int[] { newDx, newDy, newDz }, new int[] { newStartX, newStartY, newStartZ });
			if (array == null)
				goto SkipCopy;
			int lx = Math.Max(array.GetLowerBound(0), newStartX);
			int ly = Math.Max(array.GetLowerBound(1), newStartY);
			int lz = Math.Max(array.GetLowerBound(2), newStartZ);
			int copyX = Math.Min(newDx, array.GetUpperBound(0) - lx);
			int copyY = Math.Min(newDy, array.GetUpperBound(1) - ly);
			int copyZ = Math.Min(newDz, array.GetUpperBound(2) - lz);
			if (copyX <= 0 || copyY <= 0 || copyZ == 0)
				goto SkipCopy;
			ConstrainedCopy(array, lx, ly, lz, newAry, lx, ly, lz, copyX, copyY, copyZ);
		SkipCopy:
			array = newAry;
		}

		/// <summary>
		/// Constructs a new array with the given staticly-specified element type and copies the elements to the new array.
		/// The new element type must be assignable to the static element type of the array (e.g. newElementType = string with T = object[,] results in a string[,]).
		/// There must exist an assignability relation between the array's runtime type and the new type.
		/// For example, if the static type is object[,], and the runtime array type is string, and newElementType is IEnumerable&lt;char&gt;
		/// the operation can succeed.
		/// An ArrayTypeMismatchException occurs if not all of the elements of the array are assignable to the new type. This check is only required if
		/// newElementType is not safely assignable from the previous runtime element type.
		/// Note: this creates an array where it may be unsafe to assign elements according to the given static type of the array.
		/// For example, after the above example, the static type is still object[,], but if you attempted to assign an ArrayList, which would be valid
		/// at compile time, you would receive an ArrayTypeMismatchException at runtime because the array's real type is string[,] and as such it can only
		/// hold string objects.
		/// Even if the new element type is the same type as the old, the array is changed to be a new copy of the array.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array"></param>
		/// <param name="newElementType"></param>
		public static void ChangeElementType<T>(ref T array, Type newElementType)
		{
			if (!typeof(T).IsArray)
				throw new InvalidCastException("Only array types are permitted.");
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (newElementType == null)
				throw new ArgumentNullException(nameof(newElementType));
			Type et = typeof(T).GetElementType();
			if (!et.IsAssignableFrom(newElementType))
				throw new InvalidCastException("The new element type is not assignable to the static array type");
			Type ret = array.GetType().GetElementType();
			Array srcAry = (Array)(object)(array);
			int[] lbds = Enumerable.Range(0, srcAry.Rank).Select((r) => srcAry.GetLowerBound(r)).ToArray();
			int[] lens = Enumerable.Range(0, srcAry.Rank).Select((r) => srcAry.GetLength(r)).ToArray();
			Array dstAry;
			if (ret.Equals(newElementType))
				dstAry = srcAry.Duplicate();
			else
			{
				dstAry = Array.CreateInstance(newElementType, lens, lbds);
				if (ret.IsAssignableFrom(newElementType))
				{
					// Type check of existing elements is required.
					foreach (object o in srcAry)
					{
						if (!newElementType.IsInstanceOfType(o))
							throw new ArrayTypeMismatchException("An element of the array is not assignable to the new type");
					}
				}
				else if (!newElementType.IsAssignableFrom(ret))
					throw new ArrayTypeMismatchException("No assignability relation exists between the runtime element type and the new element type");
				ConstrainedCopy(srcAry, lbds, dstAry, lbds, lens); // ConstrainedCopy handles all the type-checking.
			}
		}
	}
}
