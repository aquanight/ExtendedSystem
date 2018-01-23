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
	public class UtilityTests
	{
		[TestMethod()]
		public void AsNullableTest()
		{
			Result<int, Exception> r1 = 42;
			var n1 = r1.AsNullable();
			Assert.IsTrue(n1.HasValue);
			var r2 = ((Func<int>)(() =>
			{
				throw new Exception();
			})).TryInvoke();
			var n2 = r2.AsNullable();
			Assert.IsFalse(n2.HasValue);
		}

		private void RebindTargetMethod(IAsyncResult asyncResult)
		{
		}

		[TestMethod()]
		public void RebindTest()
		{
			AsyncCallback cb = this.RebindTargetMethod;
			Action<IAsyncResult> cb2 = cb.Rebind<Action<IAsyncResult>>();

			Assert.AreEqual(cb.Target, cb2.Target);
			Assert.AreEqual(cb.Method, cb2.Method);
		}
	}
}