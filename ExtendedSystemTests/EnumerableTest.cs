using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExtendedSystemTests
{
	[TestClass]
	public class EnumerableTest
	{
		private struct TestStruct
		{
			public int foo;

			public static int bar;
		}

		private class TestClass
		{
			public int foo;

			public static int bar;
		}

		[TestMethod]
		public void TestArrayDeconstruct()
		{
			int[] ary = { 1, 3, 5, 7, 9, 11, 13 };
			// Verify out to local variables.
			int a, b, c, d, e, f, g;
			ExtendedSystem.MoreEnumerable.Deconstruct(ary, __arglist(out a, out b, out c, out d, out e, out f, out g));
			Assert.AreEqual(ary[0], a);
			Assert.AreEqual(ary[1], b);
			Assert.AreEqual(ary[2], c);
			Assert.AreEqual(ary[3], d);
			Assert.AreEqual(ary[4], e);
			Assert.AreEqual(ary[5], f);
			Assert.AreEqual(ary[6], g);

			// Verify out to array elements.
			int[] another = new int[ary.Length];
			ExtendedSystem.MoreEnumerable.Deconstruct(ary, __arglist(out another[0], out another[1], out another[2], out another[3], out another[4], out another[5], out another[6]));
			for (int i = 0; i < ary.Length; ++i)
				Assert.AreEqual(ary[i], another[i]);

			// Verify out to static fields
			ExtendedSystem.MoreEnumerable.Deconstruct(ary, __arglist(out TestStruct.bar, out TestClass.bar));
			Assert.AreEqual(ary[0], TestStruct.bar);
			Assert.AreEqual(ary[1], TestClass.bar);

			// Verify out to instance fields
			TestStruct x = new TestStruct();
			TestClass y = new TestClass();
			ExtendedSystem.MoreEnumerable.Deconstruct(ary, __arglist(out x.foo, out y.foo));
			Assert.AreEqual(ary[0], x.foo);
			Assert.AreEqual(ary[1], y.foo);
		}
	}
}
