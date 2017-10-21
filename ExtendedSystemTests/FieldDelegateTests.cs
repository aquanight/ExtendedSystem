using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExtendedSystem;

namespace ExtendedSystemTests
{
	[TestClass]
	public class FieldDelegateTests
	{
		public struct TestStruct
		{
			public int x;
			public double y;
		}

		private TestStruct a;
		private TestStruct b;

		private FieldDelegate<TestStruct, int> ts_x;
		private FieldDelegate<TestStruct, double> ts_y;

		private const int init_ax = 42;
		private const int init_bx = 12;
		private const double init_ay = 84.0;
		private const double init_by = 24.0;

		[TestInitialize]
		public void TestCreateDelegate()
		{
			this.a = new TestStruct() { x = init_ax, y = init_ay };
			this.b = new TestStruct() { x = init_bx, y = init_by };

			FieldDelegate dlg;
			dlg = FieldDelegate.CreateDelegate(typeof(TestStruct), "x");
			this.ts_x = dlg as FieldDelegate<TestStruct, int>;
			dlg = FieldDelegate.CreateDelegate(typeof(TestStruct), "y");
			this.ts_y = dlg as FieldDelegate<TestStruct, double>;
			Assert.IsNotNull(this.ts_x);
			Assert.IsNotNull(this.ts_y);
		}

		[TestMethod]
		public void TestGetValue()
		{
			TypedReference tr_a = __makeref(a);
			TypedReference tr_b = __makeref(b);

			Assert.AreEqual(42, this.ts_x.GetValue(tr_a));
			Assert.AreEqual(84.0, this.ts_y.GetValue(tr_a));
			Assert.AreEqual(12, this.ts_x.GetValue(tr_b));
			Assert.AreEqual(24.0, this.ts_y.GetValue(tr_b));
		}

		[TestMethod]
		public void TestSetValue()
		{
			TypedReference tr_a = __makeref(a);
			TypedReference tr_b = __makeref(b);

			const int new_x = 97;
			const double new_y = 34.0;

			this.ts_x.SetValue(tr_a, new_x);
			Assert.AreEqual(new_x, this.a.x);
			Assert.AreEqual(init_bx, this.b.x);
			Assert.AreEqual(init_ay, this.a.y);
			Assert.AreEqual(init_by, this.b.y);

			this.a.x = init_ax;

			this.ts_y.SetValue(tr_b, new_y);
			Assert.AreEqual(init_ax, this.a.x);
			Assert.AreEqual(init_bx, this.b.x);
			Assert.AreEqual(init_ay, this.a.y);
			Assert.AreEqual(new_y, this.b.y);
		}

		[TestMethod]
		public void TestGetValueDirect()
		{
			Assert.AreEqual(init_ax, this.ts_x.GetValueDirect(ref this.a));
			Assert.AreEqual(init_ay, this.ts_y.GetValueDirect(ref this.a));
			Assert.AreEqual(init_bx, this.ts_x.GetValueDirect(ref this.b));
			Assert.AreEqual(init_by, this.ts_y.GetValueDirect(ref this.b));
		}

		public struct TestStruct2
		{
			public string str;
			public DateTime dt;
		}

		private object _place;

		[TestMethod][ExpectedException(typeof(ArgumentException))]
		public void TestBadGetValue()
		{
			var ts2 = new TestStruct2();
			var tr = __makeref(ts2);
			this._place = this.ts_x.GetValue(tr);
		}
	}
}
