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
	/// A FieldDelegate is an unbound reference to a field of a value type. Once created, it can be combined with structures to access the indicated field by reference.
	/// </summary>
	[CLSCompliant(false)][System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711")]
	public abstract class FieldDelegate
	{
		public abstract object GetValue(TypedReference instance);

		public abstract void SetValue(TypedReference instance, object value);

		public abstract Type FieldType
		{
			get;
		}
		public abstract Type RootType
		{
			get;
		}

		[SecurityCritical]
		internal delegate ref T GetFromTypeRef<T>(TypedReference tr);

		internal static Type _helperType;
		internal static MethodInfo _helperMethod;
		private const string _helperMethodName = "FieldDelegateAssist";

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
		public static FieldDelegate CreateDelegate(params FieldInfo[] fieldChain)
		{
			if (fieldChain == null)
				throw new ArgumentNullException(nameof(fieldChain));
			// Every part of the field chain must be a value type and have sequential or explicit layout.
			if (fieldChain.Length < 1)
				throw new ArgumentException("Empty chain");
			if (fieldChain.Any((fi) => fi == null || !fi.DeclaringType.IsValueType || fi.DeclaringType.StructLayoutAttribute.Value == LayoutKind.Auto))
				throw new ArgumentException("Invalid field in chain");
			var dm = new DynamicMethod("help", typeof(IntPtr), Array.Empty<Type>(), _helperType); // Assosciate with the helper type which is security critical.
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

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
	[CLSCompliant(false)]
	public sealed class FieldDelegate<TObject, TField> : FieldDelegate where TObject : struct
	{
		private IntPtr _offset;

		private static GetFromTypeRef<TField> _helper = FieldDelegate.GetHelperMethod<TField>();

		[SecuritySafeCritical]
		static FieldDelegate()
		{
		}

		[SecuritySafeCritical]
		internal FieldDelegate(IntPtr offset)
		{
			this._offset = offset;
		}

		public override Type FieldType => typeof(TField);

		public override Type RootType => typeof(TObject);

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

		public override object GetValue(TypedReference instance)
		{
			return GetValueDirect(ref __refvalue(instance, TObject));
		}

		public override void SetValue(TypedReference instance, object value)
		{
			GetValueDirect(ref __refvalue(instance, TObject)) = (TField)value;
		}
	}
}
