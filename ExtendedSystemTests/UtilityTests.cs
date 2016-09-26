﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
			int? n1 = r1.AsNullable();
			Assert.IsTrue(n1.HasValue);
			Result<int, Exception> r2 = ((Func<int>)(() =>
			{
				throw new Exception();
			})).TryInvoke();
			int? n2 = r2.AsNullable();
			Assert.IsFalse(n2.HasValue);
		}
	}
}