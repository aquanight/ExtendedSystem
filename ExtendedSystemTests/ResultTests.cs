using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExtendedSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem.Tests
{
	[TestClass()]
	public class ResultTests
	{
		[TestMethod()]
		public void SuccessAssertTest()
		{
			var r = new Result<int, Exception>(42);
			Assert.IsTrue(r.Success);
			r.Assert(); // Should NOT throw an exception.
		}

		private double ThrowsAnException()
		{
			throw new DivideByZeroException();
		}

		[TestMethod()]
		[ExpectedException(typeof(DivideByZeroException), "Assert() should throw the exception!", AllowDerivedTypes = false)]
		public void FailedAssertTest()
		{
			var r = ((Func<double>)this.ThrowsAnException).TryInvoke();
			Assert.IsFalse(r.Success);
			r.Assert(); // Should throw DivideByZeroException
		}

		[TestMethod()]
		public void TryGetTest()
		{
			Result<double, Exception> r = 42;
			Assert.IsTrue(r.Success);
			Assert.IsTrue(r.TryGet(out double val));
			Assert.AreEqual(42, val);
			r = ((Func<double>)this.ThrowsAnException).TryInvoke();
			Assert.IsFalse(r.Success);
			Assert.IsFalse(r.TryGet(out val));
			Assert.AreEqual(default(double), val);
		}

	}
}