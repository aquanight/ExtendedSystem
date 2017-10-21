using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{
	/// <summary>
	/// Represents an unbound reference to a field of a value type. Once created, it can be combined with structures to access the indicated field by reference.
	/// </summary>
	/// <remarks>
	/// A FieldDelegate refers to some field that is part of some particular structure, but does not specifically include what specific structure instance is being referred to.
	/// Once created, a reference to a particular instance of the structure is supplied later to retrieve the field.
	/// </remarks>
	[CLSCompliant(false)][System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711")]
	public abstract class FieldDelegate
	{
		/// <summary>
		/// Retrieves from an object the value of the field represented by this FieldDelegate.
		/// </summary>
		/// <param name="instance">The object from which to retrieve the field.</param>
		/// <exception cref="ArgumentException"><paramref name="instance"/> does not refer to an object of the correct type.</exception>
		/// <returns>The current value of the field.</returns>
		public abstract object GetValue(TypedReference instance);

		/// <summary>
		/// Assigns the value to the object's field represented by this FieldDelegate
		/// </summary>
		/// <param name="instance">The object to select the field from</param>
		/// <param name="value">The value to be assigned</param>
		/// <exception cref="ArgumentException"><paramref name="instance"/> does not refer to an object of the correct type.</exception>
		/// <exception cref="ArgumentException"><paramref name="value"/> is not a type assignable to the target field</exception>
		public abstract void SetValue(TypedReference instance, object value);

		/// <summary>
		/// Returns the type associated with the field referenced by this FieldDelegate.
		/// </summary>
		public abstract Type FieldType
		{
			get;
		}
		/// <summary>
		/// Returns the type of object the field originates from.
		/// </summary>
		public abstract Type RootType
		{
			get;
		}

		[SecurityCritical]
		internal delegate ref T GetFromTypeRef<T>(TypedReference tr);

		internal static Type _helperType;
		internal static MethodInfo _helperMethod;
		private const string _helperMethodName = "FieldDelegateAssist";

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
		[SecuritySafeCritical()]
		static FieldDelegate()
		{
			ModuleBuilder mb = DynamicAssist.GetDynamicModule("fielddelegate");
			TypeBuilder tb = mb.DefineType("FieldDelegateDynamicAssist", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
			MethodBuilder mthd = tb.DefineMethod(_helperMethodName, MethodAttributes.Public | MethodAttributes.Static);
			GenericTypeParameterBuilder[] gtp = mthd.DefineGenericParameters("T");
			mthd.SetParameters(typeof(TypedReference)); // Parameter: TypedReference
			mthd.SetReturnType(gtp[0].MakeByRefType()); // Return value: ref T
			ILGenerator ilg = mthd.GetILGenerator();
			ilg.Emit(OpCodes.Ldarg_0); // (TypedReference)tr
			ilg.Emit(OpCodes.Refanyval, gtp[0]); // __refvalue(tr, T)
			ilg.Emit(OpCodes.Ret); // return ref ...
			_helperType = tb.CreateType();
			_helperMethod = _helperType.GetMethod(_helperMethodName, BindingFlags.Public | BindingFlags.Static);
		}

		[SecuritySafeCritical]
		internal static Delegate GetHelperMethod(Type target)
		{
			MethodInfo mthd = _helperMethod.MakeGenericMethod(target);
			Type dlgtype = typeof(GetFromTypeRef<>).MakeGenericType(target);
			return mthd.CreateDelegate(dlgtype);
		}

		[SecuritySafeCritical]
		internal static GetFromTypeRef<T> GetHelperMethod<T>()
		{
			return (GetFromTypeRef<T>)GetHelperMethod(typeof(T));
		}

		/// <summary>
		/// Creates a FieldDelegate referring to a particular member given as the sequence of Fields to acccess the desired data.
		/// </summary>
		/// <param name="fieldChain">The sequence of fields to access the requested data.</param>
		/// <returns>A FieldDelegate which can be used to access the data.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="fieldChain"/> is null</exception>
		/// <exception cref="ArgumentException"><paramref name="fieldChain"/> is empty</exception>
		/// <exception cref="ArgumentException"><paramref name="fieldChain"/> contains a null <see cref="FieldInfo"/> reference</exception>
		/// <exception cref="ArgumentException"><paramref name="fieldChain"/> contains a field which is not declared by a value type</exception>
		/// <exception cref="ArgumentException"><paramref name="fieldChain"/> contains a field declared by a type whose layout is neither <see cref="LayoutKind.Sequential"/>nor <see cref="LayoutKind.Explicit"/></exception>
		/// <exception cref="ArgumentException">The type stored in a field in <paramref name="fieldChain"/> does not match the declaring type of the following field</exception>
		/// <remarks>
		/// The declaring type of the first field determines the type of object which must be supplied to <see cref="GetValue(TypedReference)"/> or <see cref="SetValue(TypedReference, Object)"/>
		/// to access the referenced field. The field type of the last field determines the type of data being accessed. There can be zero or more types in between these two, in the case
		/// of value types with containing fields of other value types. Every field referenced must be an instance field, although they need not be public.
		/// </remarks>
		public static FieldDelegate CreateDelegate(params FieldInfo[] fieldChain)
		{
			if (fieldChain == null)
				throw new ArgumentNullException(nameof(fieldChain));
			// Every part of the field chain must be a value type and have sequential or explicit layout.
			if (fieldChain.Length < 1)
				throw new ArgumentException("Empty chain");
			if (fieldChain.Any((fi) => fi == null || !fi.DeclaringType.IsValueType || fi.DeclaringType.StructLayoutAttribute.Value == LayoutKind.Auto))
				throw new ArgumentException("Invalid field in chain");
			var dm = new DynamicMethod("help", typeof(IntPtr), Array.Empty<Type>(), _helperType, true); // Assosciate with the helper type which is security critical and skip visibility
			ILGenerator ilg = dm.GetILGenerator();
			Type objType = fieldChain[0].DeclaringType;
			Type fldType;
			LocalBuilder locobj = ilg.DeclareLocal(objType);
			ilg.Emit(OpCodes.Ldloca, locobj);
			ilg.Emit(OpCodes.Dup);
			ilg.Emit(OpCodes.Initobj, objType);
			for (int i = 0; i < fieldChain.Length; ++i)
			{
				if (i > 0 && fieldChain[i].DeclaringType != fieldChain[i - 1].FieldType)
					throw new ArgumentException("Incompatible fields");
				ilg.Emit(OpCodes.Ldflda, fieldChain[i]);
			}
			fldType = fieldChain.Last().FieldType;
			ilg.Emit(OpCodes.Conv_U);
			ilg.Emit(OpCodes.Ldloca, locobj);
			ilg.Emit(OpCodes.Conv_U);
			ilg.Emit(OpCodes.Sub);
			ilg.Emit(OpCodes.Ret);
			Func<IntPtr> offsetof = dm.CreateDelegate<Func<IntPtr>>();
			IntPtr off = offsetof();
			return (FieldDelegate)(typeof(FieldDelegate<,>).MakeGenericType(objType, fldType).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.DefaultBinder, new Type[] { typeof(IntPtr) }, null).Invoke(new object[] { off }));
		}

		/// <summary>
		/// Creates a FieldDelegate referring to a particular member given by the names of the fields to access the desired data.
		/// </summary>
		/// <param name="rootType">The starting type to access the field chain. This determines the type of object that must be used to access the field.</param>
		/// <param name="fieldChain">The sequence of names to access the target field.</param>
		/// <returns>A FieldDelegate which can be used to access the data.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="rootType"/> is null</exception>
		/// <exception cref="ArgumentException">The type specified by <paramref name="rootType"/> is not a value type with sequential or explicit layout</exception>
		/// <exception cref="ArgumentNullException"><paramref name="fieldChain"/> is null</exception>
		/// <exception cref="ArgumentException"><paramref name="fieldChain"/> did not specify any fields (that is, was an empty array)</exception>
		/// <exception cref="ArgumentException">The name of a field in <paramref name="fieldChain"/> could not be found in the type, or was not a public non-static field, or its type was not a value type with sequential or explicit layout</exception>
		public static FieldDelegate CreateDelegate(Type rootType, params string[] fieldChain)
		{
			if (fieldChain == null)
				throw new ArgumentNullException(nameof(fieldChain));
			// Every part of the field chain must be a value type and have sequential or explicit layout.
			if (fieldChain.Length < 1)
				throw new ArgumentException("Empty chain");
			var dm = new DynamicMethod("help", typeof(IntPtr), Array.Empty<Type>(), _helperType); // Assosciate with the helper type which is security critical.
			ILGenerator ilg = dm.GetILGenerator();
			Type objType = rootType;
			Type fldType = rootType;
			LocalBuilder locobj = ilg.DeclareLocal(objType);
			ilg.Emit(OpCodes.Ldloca, locobj);
			ilg.Emit(OpCodes.Dup);
			ilg.Emit(OpCodes.Initobj, objType);
			for (int i = 0; i < fieldChain.Length; ++i)
			{
				if (!fldType.IsValueType || fldType.StructLayoutAttribute.Value == LayoutKind.Auto)
					throw new ArgumentException("Invalid field in chain");
				FieldInfo fi = fldType.GetField(fieldChain[i], BindingFlags.Public | BindingFlags.Instance);
				if (fi == null)
					throw new ArgumentException($"Field '{fieldChain[i]}' not found in '{fldType}'");
				fldType = fi.FieldType;
				ilg.Emit(OpCodes.Ldflda, fi);
			}
			ilg.Emit(OpCodes.Conv_U);
			ilg.Emit(OpCodes.Ldloca, locobj);
			ilg.Emit(OpCodes.Conv_U);
			ilg.Emit(OpCodes.Sub);
			ilg.Emit(OpCodes.Ret);
			Func<IntPtr> offsetof = dm.CreateDelegate<Func<IntPtr>>();
			IntPtr off = offsetof();
			return (FieldDelegate)(typeof(FieldDelegate<,>).MakeGenericType(objType, fldType).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.DefaultBinder, new Type[] { typeof(IntPtr) }, null).Invoke(new object[] { off }));
		}
	}

	/// <summary>
	/// Represents an unbound reference to a field of a value type. Once created, it can be combined with structures to access the indicated field by reference.
	/// </summary>
	/// <typeparam name="TObject">The type of object containing a field.</typeparam>
	/// <typeparam name="TField">The type of the field.</typeparam>
	/// <remarks>
	/// You do not create instances of this class directly: use the <see cref="FieldDelegate.CreateDelegate(FieldInfo[])"/> or <see cref="FieldDelegate.CreateDelegate(Type, global::System.String[])"/>
	/// methods to create instances of this class.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
	[CLSCompliant(false)]
	public sealed class FieldDelegate<TObject, TField> : FieldDelegate where TObject : struct
	{
		private IntPtr _offset;

		private static GetFromTypeRef<TField> _helper = FieldDelegate.GetHelperMethod<TField>();

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
		[SecuritySafeCritical]
		static FieldDelegate()
		{
		}

		[SecuritySafeCritical]
		internal FieldDelegate(IntPtr offset)
		{
			this._offset = offset;
		}

		/// <summary>
		/// Retrieves the type of the field referred to.
		/// </summary>
		public override Type FieldType => typeof(TField);

		/// <summary>
		/// Retrieves the type of the object containing the referenced field.
		/// </summary>
		public override Type RootType => typeof(TObject);

		/// <summary>
		/// Retrieves a direct reference to the field, given a reference to an instance of the containing object.
		/// </summary>
		/// <param name="instance">A reference to an instance of the object.</param>
		/// <returns>A reference to the field within the provided object.</returns>
		[SecuritySafeCritical]
		public ref TField GetValueDirect(ref TObject instance)
		{
			TypedReference trInst = __makeref(instance);
			var fld = default(TField);
			TypedReference trFld = __makeref(fld);
			unsafe
			{
				byte* pObj = *((byte**)(&trInst)); // Extract the pointer from the TypedReference
				pObj += (long)this._offset; // Adjust to the field position
				*((byte**)(&trFld)) = pObj; // Inject into the field TypedReference
			}
			return ref _helper(trFld);
		}

		/// <summary>
		/// Retrieves from an object the value of the field represented by this FieldDelegate.
		/// </summary>
		/// <param name="instance">The object from which to retrieve the field.</param>
		/// <exception cref="ArgumentException"><paramref name="instance"/> does not refer to an object of the correct type.</exception>
		/// <returns>The current value of the field.</returns>
		public override object GetValue(TypedReference instance)
		{
			if (__reftype(instance) != typeof(TObject))
				throw new ArgumentException("Supplied object reference is not of the correct type.");
			return GetValueDirect(ref __refvalue(instance, TObject));
		}

		/// <summary>
		/// Assigns the value to the object's field represented by this FieldDelegate
		/// </summary>
		/// <param name="instance">The object to select the field from</param>
		/// <param name="value">The value to be assigned</param>
		/// <exception cref="ArgumentException"><paramref name="instance"/> does not refer to an object of the correct type.</exception>
		/// <exception cref="ArgumentException"><paramref name="value"/> is not a type assignable to the target field</exception>
		public override void SetValue(TypedReference instance, object value)
		{
			if (__reftype(instance) != typeof(TObject))
				throw new ArgumentException("Supplied object reference is not of the correct type.");
			if (!(value is TField)) // Have to do it the old way because doing as with ?? C# doesn't like on unconstrained generics :<
				throw new ArgumentException("Supplied value is not appropriate for this field.");
			GetValueDirect(ref __refvalue(instance, TObject)) = (TField)value;
		}
	}
}
