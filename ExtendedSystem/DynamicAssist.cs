using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	internal static class DynamicAssist
	{
		private static AssemblyBuilder _dynamicAssembly;

		private static AssemblyName _dynamicName;

		static DynamicAssist()
		{
			_dynamicName = new AssemblyName("dynamic");
			_dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(_dynamicName, AssemblyBuilderAccess.RunAndCollect);
			var cab = new CustomAttributeBuilder(typeof(SecurityCriticalAttribute).GetConstructor(Array.Empty<Type>()), Array.Empty<object>());
			_dynamicAssembly.SetCustomAttribute(cab);
			// By setting the dynamic helper assembly as Security Critical, everything added to it is likewise security critical, so beware of that!
		}

		internal static ModuleBuilder GetDynamicModule(string name)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			return _dynamicAssembly.GetDynamicModule(name) ?? _dynamicAssembly.DefineDynamicModule(name);
		}

	}
}
