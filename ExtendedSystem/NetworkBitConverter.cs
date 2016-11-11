using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	/// <summary>
	/// BitConverter but always puts things in network byte order. Good for ensuring proper sharing of data between systems that may or may not be the same
	/// architecture. An example might include sharing data between Windows systems (x86/amd64, little-endian) and Arduino (AVR32: big-endian).
	/// Note that if BitConverter.IsLittleEndian is false, then the entirety of this class produces results identical to standard BitConverter.
	/// The following functions are provided for interface-completeness but are functionally identical to the standard BitConverter versions:
	/// GetBytes(bool)
	/// ToBoolean(byte[])
	/// The following functions are absent because there is no formally defined network byte order for floating point values:
	/// DoubleToInt64Bits(double)
	/// GetBytes(double)
	/// GetBytes(float)
	/// Int64BitsToDouble(long)
	/// ToSingle(byte[], int)
	/// ToDouble(byte[], int)
	/// </summary>
	public static class NetworkBitConverter
	{
		public static byte[] GetBytes(bool value)
		{
			return new byte[] { (value ? (byte)1 : (byte)0) };
		}

		/// <summary>
		/// Note that uniform transmission of unicode character values might be better accomplished through the various
		/// mechanisms of System.Text.Encoding and the like (such as the various UTF-* encodings).
		/// A character encoded this way is functionally identical to one encoded by BigEndianUnicode.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static byte[] GetBytes(char value)
		{
			return GetBytes((short)value);
		}

		public static byte[] GetBytes(short value)
		{
			return GetBytes(unchecked((ushort)value));
		}

		[CLSCompliant(false)]
		public static byte[] GetBytes(ushort value)
		{
			return unchecked(new byte[]
			{
				(byte)((value & 0xFF00) >> 8),
				(byte)(value & 0xFF)
			});
		}

		public static byte[] GetBytes(int value)
		{
			return GetBytes(unchecked((uint)value));
		}

		[CLSCompliant(false)]
		public static byte[] GetBytes(uint value)
		{
			return unchecked(new byte[]
			{
				(byte)((value & 0xFF000000U) >> 24),
				(byte)((value & 0xFF0000U) >> 16),
				(byte)((value & 0xFF00U) >> 8),
				(byte)(value & 0xFFU)
			});
		}

		public static byte[] GetBytes(long value)
		{
			return GetBytes(unchecked((ulong)value));
		}

		[CLSCompliant(false)]
		public static byte[] GetBytes(ulong value)
		{
			return unchecked(new byte[]
			{
				(byte)((value & 0xFF00000000000000UL) >> 56),
				(byte)((value & 0xFF000000000000UL) >> 48),
				(byte)((value & 0xFF0000000000UL) >> 40),
				(byte)((value & 0xFF00000000UL) >> 32),
				(byte)((value & 0xFF000000UL) >> 24),
				(byte)((value & 0xFF0000UL) >> 16),
				(byte)((value & 0xFF00UL) >> 8),
				(byte)(value & 0xFFUL)
			});
		}

		public static bool ToBoolean(byte[] array, int index)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index < 0 || (index + 1) > array.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return array[index] != 0;
		}

		public static short ToInt16(byte[] array, int index)
		{
			return unchecked((short)(ToUInt16(array, index)));
		}

		[CLSCompliant(false)]
		public static ushort ToUInt16(byte[] array, int index)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index < 0 || (index + 2) > array.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return (ushort)(array[index] << 8 | array[index + 1]);
		}

		public static int ToInt32(byte[] array, int index)
		{
			return unchecked((int)ToUInt32(array, index));
		}

		[CLSCompliant(false)]
		public static uint ToUInt32(byte[] array, int index)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index < 0 || (index + 4) > array.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return (uint)(array[index] << 24 | array[index + 1] << 16 | array[index + 2] << 8 | array[index + 3]);
		}

		public static long ToInt64(byte[] array, int index)
		{
			return unchecked((long)ToUInt64(array, index));
		}

		[CLSCompliant(false)]
		public static ulong ToUInt64(byte[] array, int index)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index < 0 || (index + 8) > array.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return (ulong)(array[index] << 56 | array[index + 1] << 48 | array[index + 2] << 40 | array[index + 3] << 32 | array[index + 4] << 24 | array[index + 5] << 16 | array[index + 6] << 8 | array[index + 7]);
		}
	}
}
