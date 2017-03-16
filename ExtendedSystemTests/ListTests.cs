using System;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExtendedSystemTests
{
	[TestClass]
	public class ListTests
	{
		[TestMethod][System.Security.SecuritySafeCritical()]
		public void TestAddress()
		{
			var al = new ExtendedSystem.AddressableList<int>();
			al.Add(1);
			al.Add(2);
			al.Add(3);
			al.Add(4);
			ref int e = ref al.Address(2);
			Assert.AreEqual(3, e);
			e = 42;
			Assert.AreEqual(42, e);
			Assert.AreEqual(42, al[2]);
			// Now for a test using a DynamicMethod, which won't skip verification.
			var dynmthd = new DynamicMethod("addrtest", typeof(ExtendedSystem.AddressableList<int>), Array.Empty<Type>());
			var ilg = dynmthd.GetILGenerator();
			var loc_al = ilg.DeclareLocal(typeof(ExtendedSystem.AddressableList<int>));
			var loc_e = ilg.DeclareLocal(typeof(int).MakeByRefType());
			var addressmthd = typeof(ExtendedSystem.AddressableList<int>).GetMethod("Address");
			var addmthd = typeof(ExtendedSystem.AddressableList<int>).GetMethod("Add");
			var ctr = typeof(ExtendedSystem.AddressableList<int>).GetConstructor(Array.Empty<Type>());
			// var al = new ExtendedSystem.AddressableList<int>();
			ilg.Emit(OpCodes.Newobj, ctr);
			ilg.Emit(OpCodes.Stloc, loc_al);
			// al.Add(1);
			ilg.Emit(OpCodes.Ldloc, loc_al);
			ilg.Emit(OpCodes.Ldc_I4_1);
			ilg.Emit(OpCodes.Callvirt, addmthd);
			// al.Add(2);
			ilg.Emit(OpCodes.Ldloc, loc_al);
			ilg.Emit(OpCodes.Ldc_I4_2);
			ilg.Emit(OpCodes.Callvirt, addmthd);
			// al.Add(3);
			ilg.Emit(OpCodes.Ldloc, loc_al);
			ilg.Emit(OpCodes.Ldc_I4_3);
			ilg.Emit(OpCodes.Callvirt, addmthd);
			// al.Add(4);
			ilg.Emit(OpCodes.Ldloc, loc_al);
			ilg.Emit(OpCodes.Ldc_I4_4);
			ilg.Emit(OpCodes.Callvirt, addmthd);
			// ref int e = ref al.Address(2);
			ilg.Emit(OpCodes.Ldloc, loc_al);
			ilg.Emit(OpCodes.Ldc_I4_2);
			ilg.Emit(OpCodes.Callvirt, addressmthd);
			ilg.Emit(OpCodes.Stloc, loc_e);
			// e = 84;
			ilg.Emit(OpCodes.Ldloc, loc_e);
			ilg.Emit(OpCodes.Ldc_I4, 84);
			ilg.Emit(OpCodes.Stind_I4);
			// return al;
			ilg.Emit(OpCodes.Ldloc, loc_al);
			ilg.Emit(OpCodes.Ret);
			var dlg = (Func<ExtendedSystem.AddressableList<int>>)dynmthd.CreateDelegate(typeof(Func<ExtendedSystem.AddressableList<int>>));
			al = dlg();
			Assert.AreEqual(84, al[2]);
		}
	}
}
