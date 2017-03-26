using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;

namespace ExtendedSystem
{
	public static class ReflectEmit
	{
		/// <summary>
		/// Emit the value of a constant, provided by a literal field, into an IL stream.
		/// </summary>
		/// <param name="generator">The <see cref="ILGenerator"/> to insert the constant value into.</param>
		/// <param name="field">The field representing the constant.</param>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> or <paramref name="field"/> is null</exception>
		/// <exception cref="ArgumentException"><paramref name="field"/> is not a constant field</exception>
		/// <exception cref="InvalidOperationException"><paramref name="field"/> is a literal but its supplied literal value is not one that can be embedded in a method</exception>
		/// <remarks>
		/// A field is a constant if it has the <see cref="FieldAttributes.Literal"/> flag set, or else it has the <see cref="FieldAttributes.InitOnly"/> and <see cref="FieldAttributes.Static"/> flags set
		/// and the field has an attribute applied that designates it as a compile-time constant.
		/// </remarks>
		public static void EmitConstant(this ILGenerator generator, FieldInfo field)
		{
			if (generator == null)
				throw new ArgumentNullException(nameof(generator));
			if (field == null)
				throw new ArgumentNullException(nameof(field));
			if (field.IsLiteral)
			{
				object rawval = field.GetRawConstantValue();
				if (rawval == null)
				{
					generator.Emit(OpCodes.Ldnull);
				}
				else if (rawval is string str)
				{
					generator.Emit(OpCodes.Ldstr, str);
				}
				else if (rawval is bool || rawval is byte || rawval is sbyte || rawval is char || rawval is short || rawval is ushort || rawval is int)
				{
					int iv = Convert.ToInt32(rawval);
					generator.Emit(OpCodes.Ldc_I4, iv);
				}
				else if (rawval is uint uv)
				{
					generator.Emit(OpCodes.Ldc_I4, unchecked((int)uv));
				}
				else if (rawval is long lv)
				{
					generator.Emit(OpCodes.Ldc_I8, lv);
				}
				else if (rawval is ulong ulv)
				{
					generator.Emit(OpCodes.Ldc_I8, unchecked((long)ulv));
				}
				else if (rawval is float flv)
				{
					generator.Emit(OpCodes.Ldc_R4, flv);
				}
				else if (rawval is double dlv)
				{
					generator.Emit(OpCodes.Ldc_R8, dlv);
				}
				else
				{
					throw new InvalidOperationException("The literal value of this field is not recognizable.");
				}
			}
			else if (field.IsStatic && field.IsInitOnly)
			{
				foreach (var ad in field.GetCustomAttributesData())
				{
					if (ad.AttributeType == typeof(System.Runtime.CompilerServices.DecimalConstantAttribute))
					{
						byte scale;
						bool sign;
						int hi, mid, lo;
						var args = ad.ConstructorArguments.ToArray();
						scale = (byte)args[0].Value;
						sign = (byte)args[1].Value != 0;
						if (ad.Constructor == typeof(System.Runtime.CompilerServices.DecimalConstantAttribute).GetConstructor(new Type[] {
							typeof(byte), typeof(byte), typeof(int), typeof(int), typeof(int)
						}))
						{
							hi = (int)args[2].Value;
							mid = (int)args[3].Value;
							lo = (int)args[4].Value;
						}
						else
						{
							hi = unchecked((int)((uint)args[2].Value));
							mid = unchecked((int)((uint)args[3].Value));
							lo = unchecked((int)((uint)args[4].Value));
						}
						generator.Emit(OpCodes.Ldc_I4, lo);
						generator.Emit(OpCodes.Ldc_I4, mid);
						generator.Emit(OpCodes.Ldc_I4, hi);
						generator.Emit(sign ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
						generator.Emit(OpCodes.Ldc_I4, (int)scale);
						generator.Emit(OpCodes.Newobj, typeof(decimal).GetConstructor(new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte) }));
						return;
					}
					else if (ad.AttributeType == typeof(System.Runtime.CompilerServices.DateTimeConstantAttribute))
					{
						long data = (long)ad.ConstructorArguments[0].Value;
						generator.Emit(OpCodes.Ldc_I8, data);
						generator.Emit(OpCodes.Newobj, typeof(DateTime).GetConstructor(new Type[] { typeof(long) }));
						return;
					}
				}
				throw new ArgumentException("The field is not a constant");
			}
			else
				throw new ArgumentException("The field is not a constant");
		}

		/// <summary>
		/// Emit the sequence required to instantiate a delegate for a given method. Note that it cannot be used to create delegates of dynamic methods.
		/// If the target method is an instance method, the code previously emitted must have already placed a suitable object target on the operand stack.
		/// If the target method is a static method, the required 'ldnull' opcode will be automatically inserted, the previously emitted code need not supply it.
		/// </summary>
		/// <param name="generator">The <see cref="ILGenerator"/> to insert the delegate creation sequence into.</param>
		/// <param name="delegateType">The type of delegate to create.</param>
		/// <param name="targetMethod">The method to bind the delegate to.</param>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/>, <paramref name="delegateType"/>, or <paramref name="targetMethod"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="delegateType"/> is not a concrete (i.e. not abstract) delegate type, or is an open generic type or generic type definition.</exception>
		/// <exception cref="ArgumentException"><paramref name="targetMethod"/> is an open generic method or generic method definition.</exception>
		/// <exception cref="ArgumentException"><paramref name="targetMethod"/> is a <see cref="DynamicMethod"/></exception>
		/// <exception cref="MissingMethodException"><paramref name="delegateType"/> does not declare the required constructor</exception>
		/// <remarks>
		/// You cannot use this method to emit delegate creation for a <see cref="DynamicMethod"/> target. The IL delegate creation sequence would not hold a reference to the dynamic method, so it
		/// could not ensure the method isn't collected before the delegate is created. Instead you must store a reference to the method in a field and use that field in conjunction with the
		/// <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/> method to create the delegate.
		/// </remarks>
		public static void EmitCreateDelegate(this ILGenerator generator, Type delegateType, MethodInfo targetMethod)
		{
			if (generator == null)
				throw new ArgumentNullException(nameof(generator));
			if (delegateType == null)
				throw new ArgumentNullException(nameof(delegateType));
			if (targetMethod == null)
				throw new ArgumentNullException(nameof(targetMethod));
			if (!typeof(Delegate).IsAssignableFrom(delegateType))
				throw new ArgumentException("Type is not a delegate type.");
			if (delegateType.ContainsGenericParameters)
				throw new ArgumentException("Delegate type is open.");
			if (targetMethod.ContainsGenericParameters)
				throw new ArgumentException("Target method is open.");
			if (delegateType.IsAbstract)
				throw new ArgumentException("Delegate type is abstract.");
			var ctr = delegateType.GetConstructor(new Type[] { typeof(object), typeof(IntPtr) });
			if (ctr == null)
				throw new MissingMethodException("The delegate is missing the required Invoke method");
			if (targetMethod.IsStatic)
			{
				generator.Emit(OpCodes.Ldnull);
				generator.Emit(OpCodes.Ldftn, targetMethod);
				generator.Emit(OpCodes.Newobj, ctr);
			}
			else if (targetMethod.IsVirtual)
			{
				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Ldvirtftn, targetMethod);
				generator.Emit(OpCodes.Newobj, ctr);
			}
			else
			{
				generator.Emit(OpCodes.Ldfld, targetMethod);
				generator.Emit(OpCodes.Newobj, ctr);
			}
		}
	}
}
