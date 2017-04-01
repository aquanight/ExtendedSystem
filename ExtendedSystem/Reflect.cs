using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	public static class Reflect
	{

		/// <summary>
		/// Finds the method in a type that overrides a particular base class's virtual method.
		/// </summary>
		/// <param name="baseMethod">The base method to search for an override.</param>
		/// <param name="targetType">The type to find the overriding method in.</param>
		/// <returns>The method that overrides <paramref name="baseMethod"/> in <paramref name="targetType"/>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="baseMethod"/> or <paramref name="targetType"/> is null</exception>
		/// <exception cref="ArgumentException"><paramref name="targetType"/> is not a class or value type (that is, it is an interface).</exception>
		/// <exception cref="ArgumentException"><paramref name="targetType"/> does not implement the interface or extend the base class that declares <paramref name="baseMethod"/>.</exception>
		/// <exception cref="ArgumentException"><paramref name="baseMethod"/> is a method of a generic interface, such as <see cref="IList{T}"/>, and <paramref name="targetType"/> is an array type</exception>
		/// <remarks>
		/// This method returns the actual method that would be called by a virtual method invocation performed on the base method against an object of the specified target type.
		/// If <paramref name="baseMethod"/> is declared by an interface, the final overriding method in <paramref name="targetType"/> implementing the interface method is retrieved.
		/// (If the base class implements the interface method, and the target type overrides the interface implementation, the override is returned.)
		/// If <paramref name="baseMethod"/> is a virtual method declared by a base class, the method in <paramref name="targetType"/> which overrides it is retrieved.
		/// If no method overrides <paramref name="baseMethod"/> in <paramref name="targetType"/>, including if <paramref name="baseMethod"/> is not virtual, then <paramref name="baseMethod"/> is returned unchanged.
		/// <paramref name="targetType"/> cannot be an interface, because interfaces don't override methods.
		/// </remarks>
		public static MethodInfo GetOverridingMethod(this MethodInfo baseMethod, Type targetType)
		{
			if (baseMethod == null)
				throw new ArgumentNullException(nameof(baseMethod));
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));
			if (targetType.IsInterface)
				throw new ArgumentException("The target type is an interface and won't override any methods.");
			var dc = baseMethod.DeclaringType;
			if (dc.IsInterface)
			{
				if (dc.IsGenericType && targetType.IsArray)
					throw new ArgumentException("Methods of generic interfaces cannot be resolved for array types");
				// Search for a method implementing the interface.
				var map = targetType.GetInterfaceMap(dc);
				int ix = Array.FindIndex(map.InterfaceMethods, (m) => m.MethodHandle == baseMethod.MethodHandle);
				if (ix < 0)
					throw new InvalidOperationException("Something's gone wrong!");
				var result = map.TargetMethods[ix];
				System.Diagnostics.Debug.Assert(!result.DeclaringType.IsInterface);
				// TargetMethod may possibly only find a base class definition: re-enter the search to go up to the derived class method.
				return GetOverridingMethod(result, targetType);
			}
			else
			{
				// Fast result for non-virtual methods:
				if (!baseMethod.IsVirtual)
					return baseMethod;
				baseMethod = baseMethod.GetBaseDefinition();
				var tmthd = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				for (int mi = 0; mi < tmthd.Length; ++mi)
				{
					var tbase = tmthd[mi].GetBaseDefinition();
					if (tbase.MethodHandle == baseMethod.MethodHandle)
						return tmthd[mi];
				}
				return baseMethod;
			}
		}

		public static T GetValue<T>(this FieldInfo fieldInfo)
		{
			if (fieldInfo == null)
				throw new ArgumentNullException(nameof(fieldInfo));
			object val = fieldInfo.GetValue(null);
			return (T)val;
		}

		public static T GetValue<T>(this FieldInfo fieldInfo, object instance)
		{
			if (fieldInfo == null)
				throw new ArgumentNullException(nameof(fieldInfo));
			object val = fieldInfo.GetValue(instance);
			return (T)val;
		}

		public static T GetValueDirect<T, TObj>(this FieldInfo fieldInfo, ref TObj instance)
		{
			if (fieldInfo == null)
				throw new ArgumentNullException(nameof(fieldInfo));
			var tr = __makeref(instance);
			object val = fieldInfo.GetValueDirect(tr);
			return (T)val;
		}

		public static T CreateDelegate<T>(this MethodInfo methodInfo)
		{
			if (methodInfo == null)
				throw new ArgumentNullException(nameof(methodInfo));
			return (T)(object)methodInfo.CreateDelegate(typeof(T));
		}

		public static T CreateDelegate<T>(this MethodInfo methodInfo, object target)
		{
			if (methodInfo == null)
				throw new ArgumentNullException(nameof(methodInfo));
			return (T)(object)methodInfo.CreateDelegate(typeof(T), target);
		}

		internal static readonly Type _gtd_ienum = typeof(IEnumerable<>);
		internal static readonly Type _gtd_ilist = typeof(IList<>);
		internal static readonly Type _gtd_icoll = typeof(ICollection<>);
		internal static readonly Type _gtd_null = typeof(Nullable<>);

		private class Bound
		{
			internal HashSet<Type> _lower = new HashSet<Type>();
			internal HashSet<Type> _upper = new HashSet<Type>();
			internal HashSet<Type> _exact = new HashSet<Type>();

			internal Type ComputeSuitableType()
			{
				// If exact bounds exist then there is an exact type which must be selected.
				if (this._exact.Count > 1)
				{
					// Two different types in the exact bounds - no type can satisfy this.
					return null;
				}
				else if (this._exact.Count == 1)
				{
					var t = this._exact.Single();
					return (this._lower.All((tb) => t.IsAssignableFrom(tb)) && this._upper.All((tb) => tb.IsAssignableFrom(t))) ? t : null;
				}
				// No exact bounds, so now check the others.
				// Upper bounds are the types the target type must be able to convert *to* (i.e. Object is an upper bound of String).
				// Lower bounds are the types the target type must be able to convert *from* (i.e. String is a lower bound of Object).
				var set = new HashSet<Type>();
				set.UnionWith(this._lower);
				set.UnionWith(this._upper);
				foreach (var t2 in this._lower)
					set.RemoveWhere((t) => !t.IsAssignableFrom(t2));
				foreach (var t2 in this._upper)
					set.RemoveWhere((t) => !t2.IsAssignableFrom(t));
				var possibleResult = set.Where((t) => set.All((t2) => t2.IsAssignableFrom(t)));
				using (var e = possibleResult.GetEnumerator())
				{
					if (!e.MoveNext())
						return null;
					var result = e.Current;
					if (e.MoveNext())
						return null;
					return result;
				}
			}
		}

		// Determines if t is can be certain to be a reference type.
		private static bool IsCertainlyReferenceType(Type t)
		{
			if (!t.IsGenericParameter)
				return !t.IsValueType;
			else
			{
				if (t.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
					return true;
				return (t.BaseType != typeof(object) && !typeof(ValueType).IsAssignableFrom(t));
			}
		}

		private static void ExactInference(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
		{
			// No inference need be done unless it has generic parameters.
			if (!parameterType.ContainsGenericParameters)
				return;
			// Perform exact-type inference
			int ix;
			if ((ix = Array.IndexOf(typeParams, parameterType)) >= 0)
			{
				bounds[ix]._exact.Add(argumentType);
				return;
			}
			if (parameterType.IsArray && argumentType.IsArray && MultiArray.IsSameRank(parameterType, argumentType))
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

		private static void UpperInference(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
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
				bounds[ix]._upper.Add(argumentType);
				return;
			}
			if (parameterType.IsArray && argumentType.IsArray && MultiArray.IsSameRank(parameterType, argumentType))
			{
				// Exact inference of the array element type.
				if (IsCertainlyReferenceType(argumentType.GetElementType()))
					UpperInference(argumentType.GetElementType(), parameterType.GetElementType(), typeParams, bounds);
				else
					ExactInference(argumentType.GetElementType(), parameterType.GetElementType(), typeParams, bounds);
				return;
			}
			if (argumentType.IsGenericType)
			{
				var agtd = argumentType.GetGenericTypeDefinition();
				if (agtd.Equals(_gtd_ienum) || agtd.Equals(_gtd_icoll) || agtd.Equals(_gtd_ilist) && MultiArray.IsSZArray(parameterType))
				{
					if (IsCertainlyReferenceType(argumentType.GetGenericArguments()[0]))
						UpperInference(argumentType.GetGenericArguments()[0], parameterType.GetElementType(), typeParams, bounds);
					else
						ExactInference(argumentType.GetGenericArguments()[0], parameterType.GetElementType(), typeParams, bounds);
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
					for (var bt = parameterType; bt != null; bt = bt.BaseType)
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

		private static void LowerInference(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
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
				bounds[ix]._lower.Add(argumentType);
				return;
			}
			if (parameterType.IsArray && argumentType.IsArray && MultiArray.IsSameRank(parameterType, argumentType))
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
				var pgtd = parameterType.GetGenericTypeDefinition();
				if (pgtd.Equals(_gtd_ienum) || pgtd.Equals(_gtd_icoll) || pgtd.Equals(_gtd_ilist) && MultiArray.IsSZArray(argumentType))
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
					for (var bt = argumentType; bt != null; bt = bt.BaseType)
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

		private static void DeduceArgument(Type argumentType, Type parameterType, Type[] typeParams, Bound[] bounds)
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
		// If this is not done then it is possible you will just get null.
		// If deduction can be completed in absense of generic type constraints, but the deduced arguments cannot satisfy those constraints,
		// null is returned.
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
		public static MethodInfo InferGenericMethod(this MethodInfo genericMethod, Type[] argTypes)
		{
			if (genericMethod == null)
				throw new ArgumentNullException("genericMethod");
			if (argTypes == null)
				throw new ArgumentNullException("argTypes");
			if (!genericMethod.IsGenericMethodDefinition)
				return genericMethod;
			var ptypes = genericMethod.GetParameters().Select((pi) => pi.ParameterType).ToArray();
			if (ptypes.Length != argTypes.Length)
				return null;
			var typeParams = genericMethod.GetGenericArguments();
			var bounds = Enumerable.Range(0, argTypes.Length).Select((n) => new Bound()).ToArray();
			// Phase 1
			for (int i = 0; i < argTypes.Length; ++i)
			{
				DeduceArgument(argTypes[i], ptypes[i], typeParams, bounds);
			}
			// Phase 2
			var resultTypes = bounds.Select((b) => b.ComputeSuitableType()).ToArray();
			if (resultTypes.Any((t) => t == null))
				return null;
			try
			{
				return genericMethod.MakeGenericMethod(resultTypes);
			}
			catch
			{
				return null;
			}
		}

	}
}
