using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;
using ExtendedSystem;

namespace ExtendedSystemTests
{
	[TestClass]
	public class ReflectEmitTests
	{
		[TestMethod]
		public void TestEmitConstant()
		{
			// Test using an enumeration type:
			var sourceType = typeof(ConsoleColor);
			var constantField = sourceType.GetField("Blue", BindingFlags.Public | BindingFlags.Static);
			Assert.IsTrue(constantField.IsLiteral);
			var asmname = new AssemblyName("testassembly");
			var asmbld = AssemblyBuilder.DefineDynamicAssembly(asmname, AssemblyBuilderAccess.RunAndCollect);
			var modbld = asmbld.DefineDynamicModule("testassembly.dll");
			var tbld = modbld.DefineType("EmitTestType", TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Public);
			var mthdbld = tbld.DefineMethod("TestMethod", MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(ConsoleColor), Array.Empty<Type>());
			var ilg = mthdbld.GetILGenerator();
			ilg.EmitConstant(constantField);
			ilg.Emit(OpCodes.Ret);
			var tt = tbld.CreateType();
			var mthd = tt.GetMethod("TestMethod");
			var dlg = mthd.CreateDelegate<Func<ConsoleColor>>();
			ConsoleColor clr = dlg();
			Assert.AreEqual(ConsoleColor.Blue, clr);
			var body = mthd.GetMethodBody();
			var il = body.GetILAsByteArray();
			var expil = new byte[] { unchecked((byte)OpCodes.Ldc_I4.Value), (byte)ConsoleColor.Blue, 0, 0, 0, unchecked((byte)OpCodes.Ret.Value) };
			CollectionAssert.AreEqual(expil, il);
		}
	}
}
