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
		private static bool CanConvert(Type from, Type to)
		{
			if (to.IsAssignableFrom(from))
				return true;
			if (from.Equals(typeof(char)))
				return (to.Equals(typeof(ushort)) || to.Equals(typeof(uint)) || to.Equals(typeof(int)) || to.Equals(typeof(ulong)) || to.Equals(typeof(long)) || to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(byte)))
				return (to.Equals(typeof(char)) || to.Equals(typeof(ushort)) || to.Equals(typeof(short)) || to.Equals(typeof(uint)) || to.Equals(typeof(int)) || to.Equals(typeof(ulong)) || to.Equals(typeof(long)) || to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(sbyte)))
				return (to.Equals(typeof(short)) || to.Equals(typeof(int))  || to.Equals(typeof(long)) || to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(ushort)))
				return (to.Equals(typeof(uint)) || to.Equals(typeof(int)) || to.Equals(typeof(ulong)) || to.Equals(typeof(long)) || to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(short)))
				return (to.Equals(typeof(int)) || to.Equals(typeof(long)) || to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(uint)))
				return (to.Equals(typeof(ulong)) || to.Equals(typeof(long)) || to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(int)))
				return (to.Equals(typeof(long)) || to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(ulong)))
				return (to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(long)))
				return (to.Equals(typeof(float)) || to.Equals(typeof(double)));
			if (from.Equals(typeof(float)))
				return (to.Equals(typeof(double)));
			return false;
		}

		private class Bound
		{
			internal List<Type> lower = new List<Type>();
			internal List<Type> upper = new List<Type>();
			internal List<Type> exact = new List<Type>();

			internal Type ComputeSuitableType()
			{
				exact = exact.Distinct().ToList();
				// If exact bounds exist then there is an exact type which must be selected.
				if (exact.Count > 1)
				{
					// Two different types in the exact bounds - no type can satisfy this.
					return null;
				}
				else if (exact.Count == 1)
				{
					Type t = exact[0];
					return (lower.All((tb) => CanConvert(tb, t)) && upper.All((tb) => CanConvert(t, tb))) ? t : null;
				}
				// No exact bounds, so now check the others.
				// Upper bounds are the types the target type must be able to convert *to* (i.e. Object is an upper bound of String).
				// Lower bounds are the types the target type must be able to convert *from* (i.e. String is a lower bound of Object).
				// *Sigh* I really don't much like doing it this way, but the way type inference works in C# pretty much requires it.
				// A compiler can make the excuse, but this potentially can be used by things like PowerShell if you somehow replaced the DefaultBinder
				// so I'd like to not be slow if possible.
				var set = AppDomain.CurrentDomain.GetAssemblies().SelectMany((a) => a.GetTypes()).Where((t) =>
					lower.All((t2) => CanConvert(t2, t)) && upper.All((t2) => CanConvert(t, t2))
				);
				var possibleResult = set.Where((t) => set.All((t2) => CanConvert(t, t2)));
				var e = possibleResult.GetEnumerator();
				if (!e.MoveNext())
					return null;
				Type result = e.Current;
				if (e.MoveNext())
					return null;
				return result;
			}
		}

		private void ExactInference(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
		{
			// No inference need be done unless it has generic parameters.
			if (!parameterType.ContainsGenericParameters)
				return;
			// Perform exact-type inference
			int ix;
			if ((ix = Array.IndexOf(typeParams, parameterType)) >= 0)
			{
				bounds[ix].exact.Add(argumentType);
				return;
			}
			if (parameterType.IsArray && argumentType.IsArray && parameterType.GetArrayRank() == argumentType.GetArrayRank())
			{
				// Exact inference of the array element type.
				ExactInference(argumentType.GetElementType(), parameterType.GetElementType(), typeParams, bounds);
				return;
			}
			if (parameterType.IsGenericType && argumentType.IsConstructedGenericType && parameterType.GetGenericTypeDefinition().Equals(argumentType.GetGenericTypeDefinition()))
			{
				var pgp = parameterType.GetGenericArguments();
				var agp = argumentType.GetGenericArguments();
				if (pgp.Length == agp.Length)
				{
					foreach (var o in pgp.Zip(agp, (pt, at) => new
					{
						pt = pt,
						at = at
					}))
						ExactInference(o.at, o.pt, typeParams, bounds);
					return;
				}
			}
		}

		private void UpperInference(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
		{
			// NOTE: argumentType is not necessarily a runtime-live type (i.e. a concrete class). While all actual objects passed by reflection
			// will be of a concrete class type, argumentType can sometimes be nonconcrete. Consider, for example, an object of type List<IEnumerable<int>>.
			// If this is passed to a generic method that is List<T>, then we need to deduce T -> IEnumerable<int>, so argumentType will be IEnumerable<int>.
			// No inference need be done unless it has generic parameters.
			if (!parameterType.ContainsGenericParameters)
				return;
			// Perform exact-type inference
			int ix;
			if ((ix = Array.IndexOf(typeParams, parameterType)) >= 0)
			{
				bounds[ix].upper.Add(argumentType);
				return;
			}
			if (parameterType.IsArray && argumentType.IsArray && parameterType.GetArrayRank() == argumentType.GetArrayRank())
			{
				// Exact inference of the array element type.
				if (argumentType.GetElementType().IsValueType)
					ExactInference(argumentType.GetElementType(), parameterType.GetElementType(), typeParams, bounds);
				else
					UpperInference(argumentType.GetElementType(), parameterType.GetElementType(), typeParams, bounds);
				return;
			}
			if (argumentType.IsGenericType)
			{
				Type agtd = argumentType.GetGenericTypeDefinition();
				if (agtd.Equals(typeof(IEnumerable<int>).GetGenericTypeDefinition()) || agtd.Equals(typeof(ICollection<int>).GetGenericTypeDefinition()) || agtd.Equals(typeof(IList<int>).GetGenericTypeDefinition()) && parameterType.IsArray && parameterType.GetArrayRank() == 1)
				{
					if (argumentType.GetGenericArguments()[0].IsValueType)
						ExactInference(argumentType.GetGenericArguments()[0], parameterType.GetElementType(), typeParams, bounds);
					else
						UpperInference(argumentType.GetGenericArguments()[0], parameterType.GetElementType(), typeParams, bounds);
					return;
				}
				if (agtd.IsInterface)
				{
					var ifs = (parameterType.IsInterface ? Enumerable.Repeat(parameterType, 1) : Enumerable.Empty<Type>()).Concat(parameterType.GetInterfaces()).Where((i) => i.IsGenericType && i.GetGenericTypeDefinition().Equals(agtd));
					if (ifs.Count() == 1)
					{
						foreach (var o in parameterType.GetGenericArguments().Zip(ifs.First().GetGenericArguments(), (pt, at) => new
						{
							pt = pt,
							at = at
						}))
						{
							if (o.at.IsValueType)
								ExactInference(o.at, o.pt, typeParams, bounds);
							else if (o.pt.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Covariant))
								UpperInference(o.at, o.pt, typeParams, bounds);
							else if (o.pt.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Contravariant))
								LowerInference(o.at, o.pt, typeParams, bounds);
							else
								ExactInference(o.at, o.pt, typeParams, bounds);
						}
						return;
					}
				}
				else if (!parameterType.IsInterface)
				{
					for (Type bt = parameterType; bt != null; bt = bt.BaseType)
					{
						if (bt.GetGenericTypeDefinition().Equals(agtd))
						{
							foreach (var o in parameterType.GetGenericArguments().Zip(bt.GetGenericArguments(), (pt, at) => new
							{
								pt = pt,
								at = at
							}))
								ExactInference(o.at, o.pt, typeParams, bounds); // Class type parameters cannot be variant.
							return;
						}
					}
				}
			}
		}

		private void LowerInference(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
		{
			// NOTE: argumentType is not necessarily a runtime-live type (i.e. a concrete class). While all actual objects passed by reflection
			// will be of a concrete class type, argumentType can sometimes be nonconcrete. Consider, for example, an object of type List<IEnumerable<int>>.
			// If this is passed to a generic method that is List<T>, then we need to deduce T -> IEnumerable<int>, so argumentType will be IEnumerable<int>.
			// No inference need be done unless it has generic parameters.
			if (!parameterType.ContainsGenericParameters)
				return;
			// Perform exact-type inference
			int ix;
			if ((ix = Array.IndexOf(typeParams, parameterType)) >= 0)
			{
				bounds[ix].lower.Add(argumentType);
				return;
			}
			if (parameterType.IsArray && argumentType.IsArray && parameterType.GetArrayRank() == argumentType.GetArrayRank())
			{
				// Exact inference of the array element type.
				if (argumentType.GetElementType().IsValueType)
					ExactInference(argumentType.GetElementType(), parameterType.GetElementType(), typeParams, bounds);
				else
					LowerInference(argumentType.GetElementType(), parameterType.GetElementType(), typeParams, bounds);
				return;
			}
			if (parameterType.IsGenericType)
			{
				Type pgtd = parameterType.GetGenericTypeDefinition();
				if (pgtd.Equals(typeof(IEnumerable<int>).GetGenericTypeDefinition()) || pgtd.Equals(typeof(ICollection<int>).GetGenericTypeDefinition()) || pgtd.Equals(typeof(IList<int>).GetGenericTypeDefinition()) && argumentType.IsArray && argumentType.GetArrayRank() == 1)
				{
					if (argumentType.GetElementType().IsValueType)
						ExactInference(argumentType.GetElementType(), parameterType.GetGenericArguments()[0], typeParams, bounds);
					else
						LowerInference(argumentType.GetElementType(), parameterType.GetGenericArguments()[0], typeParams, bounds);
					return;
				}
				if (pgtd.IsInterface)
				{
					var ifs = (argumentType.IsInterface ? Enumerable.Repeat(argumentType, 1) : Enumerable.Empty<Type>()).Concat(argumentType.GetInterfaces()).Where((i) => i.IsGenericType && i.GetGenericTypeDefinition().Equals(pgtd));
					if (ifs.Count() == 1)
					{
						foreach (var o in parameterType.GetGenericArguments().Zip(ifs.First().GetGenericArguments(), (pt, at) => new
						{
							pt = pt,
							at = at
						}))
						{
							if (o.at.IsValueType)
								ExactInference(o.at, o.pt, typeParams, bounds);
							else if (o.pt.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Covariant))
								LowerInference(o.at, o.pt, typeParams, bounds);
							else if (o.pt.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Contravariant))
								UpperInference(o.at, o.pt, typeParams, bounds);
							else
								ExactInference(o.at, o.pt, typeParams, bounds);
						}
						return;
					}
				}
				else if (!argumentType.IsInterface)
				{
					for (Type bt = argumentType; bt != null; bt = bt.BaseType)
					{
						if (bt.GetGenericTypeDefinition().Equals(pgtd))
						{
							foreach (var o in parameterType.GetGenericArguments().Zip(bt.GetGenericArguments(), (pt, at) => new
							{
								pt = pt,
								at = at
							}))
								ExactInference(o.at, o.pt, typeParams, bounds); // Class type parameters cannot be variant.
							return;
						}
					}
				}
			}
		}

		private void DeduceArgument(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
		{
			if (parameterType.IsByRef)
				ExactInference(argumentType, parameterType.GetElementType(), typeParams, bounds);
			else
				LowerInference(argumentType, parameterType, typeParams, bounds);
		}

		// Attempt to instantiate a generic method from the argument list.
		// If the method isn't a generic method definition, return it as-is.
		// If we can't complete the deduction, return null.
		// Note: argTypes must already be transformed according to:
		// 1) Collapsing of additional arguments into a final args array.
		// 2) Appending or inserting of unspecified optional arguments.
		// 3) Reordering of arguments to account for named arguments.
		// If this is not done then it is possible an ArgumentException will be thrown.
		// If deduction can be completed in absense of generic type constraints, but the deduced arguments cannot satisfy those constraints,
		// an ArgumentException occurs.
		private MethodInfo InstantiateGenericFromArguments(MethodInfo genericMethod, Type[] argTypes)
		{
			if (genericMethod == null)
				throw new ArgumentNullException("genericMethod");
			if (argTypes == null)
				throw new ArgumentNullException("argTypes");
			if (!genericMethod.IsGenericMethodDefinition)
				return genericMethod;
			Type[] ptypes = genericMethod.GetParameters().Select((pi) => pi.ParameterType).ToArray();
			if (ptypes.Length != argTypes.Length) throw new ArgumentException("argument count mismatch", "argTypes");
			Type[] typeParams = genericMethod.GetGenericArguments();
			Bound[] bounds = Enumerable.Range(0, argTypes.Length).Select((n) => new Bound()).ToArray();
			// Phase 1
			for (int i = 0; i < argTypes.Length; ++i)
			{
				DeduceArgument(argTypes[i], ptypes[i], typeParams, bounds);
			}
			// Phase 2
			Type[] resultTypes = bounds.Select((b) => b.ComputeSuitableType()).ToArray();
			if (resultTypes.Any((t) => t == null))
				return null;
			return genericMethod.MakeGenericMethod(resultTypes);
		}

		// Helper object to do the heavy lifiting after generic instantiation.
		private static Binder stdbind = Type.DefaultBinder;

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
			throw new NotSupportedException("ChangeType is not needed I guess.");
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
			MethodBase[] methods = match.Duplicate();
			int ix;
			while ((ix = Array.FindIndex(methods, (m) => m.IsGenericMethodDefinition)) >= 0)
			{
				MethodInfo m = methods[ix] as MethodInfo;
				if (m == null)
				{
					methods[ix] = null;
					continue;
				}
				Type[] consideration = types.Duplicate();
				m = InstantiateGenericFromArguments(m, consideration);
				methods[ix] = m;
			}
			// Remove the unsuitables entirely:
			methods = methods.Where((m) => (m != null)).ToArray();
			return stdbind.SelectMethod(bindingAttr, methods, types, modifiers);
		}

		public override PropertyInfo SelectProperty(BindingFlags bindingAttr, PropertyInfo[] match, Type returnType, Type[] indexes, ParameterModifier[] modifiers)
		{
			// Properties cannot be generic.
			return stdbind.SelectProperty(bindingAttr, match, returnType, indexes, modifiers);
		}
	}
}
