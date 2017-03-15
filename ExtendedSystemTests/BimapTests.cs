using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExtendedSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ExtendedSystem.Tests
{
	[TestClass()]
	public class BimapTests
	{
		[TestMethod()]
		public void RehashTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			map.Rehash(false);
			map.Rehash(true);
		}

		private class CustomComparer<T> : IEqualityComparer<T>
		{
			public bool Equals(T x, T y)
			{
				return x.Equals(y);
			}

			public int GetHashCode(T obj)
			{
				return obj.GetHashCode();
			}
		}

		[TestMethod()]
		public void BimapTest()
		{
			// Test the default constructor
			var map = new Bimap<int, char>();
			// Test the capacity constructor
			map = new Bimap<int, char>(44);
			Assert.IsTrue(map.Capacity >= 44);
			// Test the comparer constructor
			var intCmp = new CustomComparer<int>();
			var chrCmp = new CustomComparer<char>();
			map = new Bimap<int, char>(intCmp, chrCmp);
			Assert.AreSame(intCmp, map.LeftComparer);
			Assert.AreSame(chrCmp, map.RightComparer);
			// Test the dictionary constructor
			var srcDict = new Dictionary<int, char>();
			srcDict.Add(65, 'a');
			srcDict.Add(97, 'q');
			srcDict.Add(-44, 'x');
			map = new Bimap<int, char>(srcDict);
			// Don't check the contents: the Add/AddRange tests will do that. Just make sure the constructor doesn't fail.
		}

		[TestMethod()]
		public void AddTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Assert.AreEqual(3, map.Count);
		}

		[TestMethod()]
		[ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
		public void AddTest1()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(65, 'a');
		}

		[TestMethod()]
		public void AddRangeTest()
		{
			var map = new Bimap<int, char>();
			var srcDict = new Dictionary<int, char>();
			srcDict.Add(65, 'a');
			srcDict.Add(97, 'q');
			srcDict.Add(-44, 'x');
			map.AddRange(srcDict);
			Assert.AreEqual(3, map.Count);
		}

		[TestMethod()]
		[ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
		public void AddRangeTest1()
		{
			KeyPair<int, char>[] src = { new KeyPair<int, char>(65, 'a'), new KeyPair<int, char>(65, 'a') };
			var map = new Bimap<int, char>();
			map.AddRange(src);
		}

		[TestMethod()]
		public void ClearTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Assert.AreEqual(3, map.Count);
			map.Clear();
			Assert.AreEqual(0, map.Count);
			Assert.IsFalse(map.Any());
		}

		[TestMethod()]
		public void ContainsTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Assert.IsTrue(map.Contains(new KeyPair<int, char>(65, 'a')));
			Assert.IsTrue(map.Contains(new KeyPair<int, char>(97, 'q')));
			Assert.IsTrue(map.Contains(new KeyPair<int, char>(-44, 'x')));
			Assert.IsFalse(map.Contains(new KeyPair<int, char>(65, 'x')));
			Assert.IsFalse(map.Contains(new KeyPair<int, char>(97, 'a')));
			Assert.IsFalse(map.Contains(new KeyPair<int, char>(-44, 'q')));
		}

		[TestMethod()]
		public void ContainsKeyTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Assert.IsTrue(map.ContainsKey(65));
			Assert.IsTrue(map.ContainsKey(97));
			Assert.IsTrue(map.ContainsKey(-44));
			Assert.IsTrue(map.ContainsKey('q'));
			Assert.IsTrue(map.ContainsKey('a'));
			Assert.IsTrue(map.ContainsKey('x'));
			Assert.IsFalse(map.ContainsKey(0));
			Assert.IsFalse(map.ContainsKey(-97));
			Assert.IsFalse(map.ContainsKey('\0'));
			Assert.IsFalse(map.ContainsKey('#'));
		}

		[TestMethod()]
		public void CopyToTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			var array = new KeyPair<int, char>[map.Count];
			map.CopyTo(array, 0);
			Assert.IsTrue(array.Any((kp) => kp.Left == 65));
			Assert.IsTrue(array.Any((kp) => kp.Left == 97));
			Assert.IsTrue(array.Any((kp) => kp.Left == -44));
			Assert.IsTrue(array.Any((kp) => kp.Right == 'a'));
			Assert.IsTrue(array.Any((kp) => kp.Right == 'q'));
			Assert.IsTrue(array.Any((kp) => kp.Right == 'x'));

			Assert.IsTrue(Array.FindIndex(array, (kp) => kp.Left == 65) == Array.FindIndex(array, (kp) => kp.Right == 'a'));
			Assert.IsTrue(Array.FindIndex(array, (kp) => kp.Left == 97) == Array.FindIndex(array, (kp) => kp.Right == 'q'));
			Assert.IsTrue(Array.FindIndex(array, (kp) => kp.Left == -44) == Array.FindIndex(array, (kp) => kp.Right == 'x'));
		}

		[TestMethod()]
		public void GetEnumeratorTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');

			// Test GetEnumerator via LINQ!
			Assert.IsTrue(map.Any((kp) => kp.Left == 65));
			Assert.IsFalse(map.All((kp) => kp.Left > 0));
			Assert.AreEqual((65 + 97 - 44) / 3.0, map.Average((kp) => kp.Left));
			Assert.AreEqual(('a' + 'q' + 'x') / 3.0, map.Average((kp) => kp.Right));
		}

		[TestMethod()]
		public void RemoveTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Assert.IsTrue(map.ContainsKey(65));
			Assert.IsTrue(map.Remove(65));
			Assert.IsFalse(map.ContainsKey(65) || map.ContainsKey('a'));
			Assert.IsFalse(map.Remove(65) || map.Remove('a'));
			Assert.IsTrue(map.ContainsKey('x'));
			Assert.IsTrue(map.Remove('x'));
			Assert.IsFalse(map.ContainsKey('x') || map.ContainsKey(-44));
			Assert.IsFalse(map.Remove('x') || map.Remove(-44));
			Assert.IsTrue(map.Contains(new KeyPair<int, char>(97, 'q')));
			Assert.IsFalse(map.Contains(new KeyPair<int, char>(98, 'q')));
			Assert.IsFalse(map.Contains(new KeyPair<int, char>(97, 'r')));
			Assert.IsFalse(map.Remove(new KeyPair<int, char>(98, 'q')));
			Assert.IsFalse(map.Remove(new KeyPair<int, char>(97, 'r')));
			Assert.IsTrue(map.Remove(new KeyPair<int, char>(97, 'q')));
			Assert.IsFalse(map.Contains(new KeyPair<int, char>(97, 'q')));
			Assert.IsFalse(map.Remove(new KeyPair<int, char>(97, 'q')));
			// It should be empty now
			Assert.IsFalse(map.Any());
		}

		[TestMethod()]
		public void TryGetValueTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Assert.IsTrue(map.TryGetValue(65, out char r));
			Assert.AreEqual('a', r);
			Assert.IsTrue(map.TryGetValue('x', out int l));
			Assert.AreEqual(-44, l);
			Assert.IsFalse(map.TryGetValue(99, out r));
			Assert.AreEqual(default(char), r);
			Assert.IsFalse(map.TryGetValue('z', out l));
			Assert.AreEqual(default(int), l);
		}

		[TestMethod()]
		public void GetValueTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Assert.AreEqual('a', map[65]);
			Assert.AreEqual('q', map[97]);
			Assert.AreEqual('x', map[-44]);
			Assert.AreEqual(65, map['a']);
			Assert.AreEqual(97, map['q']);
			Assert.AreEqual(-44, map['x']);
		}

		[TestMethod()]
		[ExpectedException(typeof(KeyNotFoundException), AllowDerivedTypes = false)]
		public void GetBadValueTest()
		{
			var map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			char ch = map[96];
		}

		/*
		[TestMethod()]
		public void InvertTest()
		{
			Bimap<int, char> map = new Bimap<int, char>();
			map.Add(new KeyPair<int, char>(65, 'a'));
			map.Add(97, 'q');
			map.Add(-44, 'x');
			Bimap<char, int> inv = map.Invert();
			Assert.IsTrue(map.All((kp) => inv.Contains(new KeyPair<char, int>(kp.Right, kp.Left))));
			Assert.IsTrue(inv.All((kp) => map.Contains(new KeyPair<int, char>(kp.Right, kp.Left))));
		}
		*/

		[TestMethod()]
		public void LoadTest()
		{
			var rng = new Random();
			Func<int> intGen = () =>
			{
				var b = new byte[4];
				rng.NextBytes(b);
				return BitConverter.ToInt32(b, 0);
			};
			Func<string> strGen = () =>
			{
				int len = rng.Next(16) + 1;
				return new string(Enumerable.Range(0, len).Select((i) => (char)(rng.Next(0x20, 0x7F))).ToArray());
			};

			var stpw = Stopwatch.StartNew();
			var map = new Bimap<string, int>(1024);
			Trace.TraceInformation("Map construction: {0} ms", stpw.SplitMilliseconds());
			// Small load test.
			while (map.Count < 1000)
			{
				try
				{
					map.Add(strGen(), intGen());
				}
				catch (ArgumentException)
				{
				}
			}
			Trace.TraceInformation("One thousand elements: {0} ms", stpw.SplitMilliseconds());
			map.Rehash(true);
			Trace.TraceInformation("First rehash: {0} ms", stpw.SplitMilliseconds());
			map.Capacity = 16384;
			Trace.TraceInformation("Enlargement: {0} ms", stpw.SplitMilliseconds());
			map.Rehash(true);
			Trace.TraceInformation("Second rehash: {0} ms", stpw.SplitMilliseconds());
			map.Capacity = 16;
			Trace.TraceInformation("Shrink: {0} ms", stpw.SplitMilliseconds());
			map.Rehash(true);
			Trace.TraceInformation("Third rehash: {0} ms", stpw.SplitMilliseconds());
			map.Clear();
			Trace.TraceInformation("Wipe: {0} ms", stpw.SplitMilliseconds());
			map.Capacity = 0;
			Trace.TraceInformation("Post-wipe shrink: {0} ms", stpw.SplitMilliseconds());

			// Large load test.
			while (map.Count < 1000000)
			{
				try
				{
					map.Add(strGen(), intGen());
				}
				catch (ArgumentException)
				{
				}
			}
			Trace.TraceInformation("One million random elements: {0} ms", stpw.SplitMilliseconds());
			map.Rehash(true);
			Trace.TraceInformation("First rehash: {0} ms", stpw.SplitMilliseconds());
			map.Capacity = 16384;
			Trace.TraceInformation("Enlargement: {0} ms", stpw.SplitMilliseconds());
			map.Rehash(true);
			Trace.TraceInformation("Second rehash: {0} ms", stpw.SplitMilliseconds());
			map.Capacity = 16;
			Trace.TraceInformation("Shrink: {0} ms", stpw.SplitMilliseconds());
			map.Rehash(true);
			Trace.TraceInformation("Third rehash: {0} ms", stpw.SplitMilliseconds());
			map.Clear();
			Trace.TraceInformation("Wipe: {0} ms", stpw.SplitMilliseconds());
			map.Capacity = 0;
			Trace.TraceInformation("Post-wipe shrink: {0} ms", stpw.SplitMilliseconds());
		}
	}
}