using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExtendedSystemTests
{
	[TestClass]
	public class ReflectTest
	{
		[TestMethod]
		public void TestGetOverridingMethod()
		{
			var tsrc = typeof(object);
			var tdst = typeof(string);
			var srcmthd = tsrc.GetMethod("Equals", new Type[] { typeof(object) });
			var dstmthd = tdst.GetMethod("Equals", new Type[] { typeof(object) });
			Assert.AreNotEqual(srcmthd.MethodHandle, dstmthd.MethodHandle);
			Assert.AreEqual(srcmthd.MethodHandle, dstmthd.GetBaseDefinition().MethodHandle);
			var ovdmthd = ExtendedSystem.Reflect.GetOverridingMethod(srcmthd, tdst);
			Assert.AreEqual(dstmthd.MethodHandle, ovdmthd.MethodHandle);
		}
	}
}
