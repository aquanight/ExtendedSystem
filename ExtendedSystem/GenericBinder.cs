using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	/// <summary>
	/// Generic-friendly Binder that can resolve parameters against generic methods. Uses rules similar to those used by the C# compiler.
	/// </summary>
	public class GenericBinder : Binder
	{
		// Helper object to do the heavy lifiting after generic instantiation.
		private static Binder _stdbind = Type.DefaultBinder;

		public override FieldInfo BindToField(BindingFlags bindingAttr, FieldInfo[] match, object value, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public override MethodBase BindToMethod(BindingFlags bindingAttr, MethodBase[] match, ref object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] names, out object state)
		{
			throw new NotImplementedException();
		}

		public override object ChangeType(object value, Type type, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public override void ReorderArgumentArray(ref object[] args, object state)
		{
			throw new NotImplementedException();
		}

		public override MethodBase SelectMethod(BindingFlags bindingAttr, MethodBase[] match, Type[] types, ParameterModifier[] modifiers)
		{
			if (match == null)
				throw new ArgumentNullException("match");
			if (types == null)
				throw new ArgumentNullException("types");
			var methods = match.Duplicate();
			int ix;
			while ((ix = Array.FindIndex(methods, (m) => m.IsGenericMethodDefinition)) >= 0)
			{
				var m = methods[ix] as MethodInfo;
				if (m == null)
				{
					methods[ix] = null;
					continue;
				}
				var consideration = types.Duplicate();
				m = m.InferGenericMethod(consideration);
				methods[ix] = m;
			}
			// Remove the unsuitables entirely:
			methods = methods.Where((m) => (m != null)).ToArray();
			return _stdbind.SelectMethod(bindingAttr, methods, types, modifiers);
		}

		public override PropertyInfo SelectProperty(BindingFlags bindingAttr, PropertyInfo[] match, Type returnType, Type[] indexes, ParameterModifier[] modifiers)
		{
			// Properties cannot be generic.
			return _stdbind.SelectProperty(bindingAttr, match, returnType, indexes, modifiers);
		}
	}
}
