using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Linq;

namespace ExtendedSystem
{
	public static class Utility
	{
		//Because bloody IClonable couldn't typesafe it.
		public static T Duplicate<T>(this T obj) where T : ICloneable
		{
			return (T)obj.Clone();
		}

		// A simple func which returns its argument. The main use of this is when you need it for callbacks.
		public static T AsIs<T>(T value)
		{
			return value;
		}

		public static TValue? AsNullable<TValue, TException>(this Result<TValue, TException> result) where TValue : struct where TException : Exception
		{
			if (result == null)
				throw new ArgumentNullException("result");
			if (result.Success)
				return result.Assert();
			else
				return null;
		}

		public static Action BindFirst<T>(this Action<T> action, T first)
		{
			return () => action(first);
		}

		public static Action<T2> BindFirst<T1, T2>(this Action<T1, T2> action, T1 first)
		{
			return (T2 second) => action(first, second);
		}

		public static Func<TResult> BindFirst<T, TResult>(this Func<T, TResult> func, T first)
		{
			return () => func(first);
		}

		public static Func<T2, TResult> BindFirst<T1, T2, TResult>(this Func<T1, T2, TResult> func, T1 first)
		{
			return (T2 second) => func(first, second);
		}

		public static Delegate BindFirst(Delegate @delegate, Type newDelegate, object value)
		{
			if (@delegate == null)
				throw new ArgumentNullException(nameof(@delegate));
			if (newDelegate == null)
				throw new ArgumentNullException(nameof(newDelegate));
			var mthd = @delegate.Method;
			object tgt = @delegate.Target;
			Type[] sig = @delegate.GetType().GetMethod("Invoke").GetParameters().Select((pi) => pi.ParameterType).ToArray();
			if (sig.Length < 1)
				throw new InvalidOperationException("There is no parameter to bind.");
			if (!sig[0].IsInstanceOfType(value))
				throw new ArgumentException("The value cannot be bound to the type of the first parameter.");
			// .NET delgates can bind static methods with a target (which fills the first parameter), or instance methods with no target (which adds a first
			// parameter). We have to account for these scenarios.
			if (tgt == null)
			{
				// We can bind the delegate type directly, no need involve S.Linq.Expressions...
				return Delegate.CreateDelegate(newDelegate, value, mthd);
			}
			else
			{
				// We need to keep both the supplied first-argument and the new bound argument...
				ConstantExpression dlgTarget = Expression.Constant(tgt);
				ConstantExpression boundParam = Expression.Constant(value, sig[0]);
				Type[] newSig = sig.Skip(1).ToArray();
				ParameterExpression[] param = newSig.Select((t) => Expression.Parameter(t)).ToArray();
				MethodCallExpression callexp;
				if (mthd.IsStatic)
				{
					Expression[] arguments = (new Expression[] { dlgTarget, boundParam }).Concat(param).ToArray();
					callexp = Expression.Call(mthd, arguments);
				}
				else
				{
					Expression[] arguments = Enumerable.Repeat<Expression>(boundParam, 1).Concat(param).ToArray();
					callexp = Expression.Call(dlgTarget, mthd, arguments);
				}
				return Expression.Lambda(newDelegate, callexp, true, param).Compile();
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "h")]
		[System.Security.SecurityCritical()]
		public static Result<int, Exception> FromHResult(int hResult)
		{
			if ((hResult & 0x80000000) != 0)
			{
				var e = System.Runtime.InteropServices.Marshal.GetExceptionForHR(hResult);
				return Result<int, Exception>.FromException(e);
			}
			else
				return hResult;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
		public static Result<T, Exception> TryInvoke<T>(this Func<T> func)
		{
			if (func == null)
				throw new ArgumentNullException("func");
			try
			{
				return func();
			}
			catch (Exception e)
			{
				return Result<T, Exception>.FromException(e);
			}
		}

		public static Result<TValue, TException> TryInvoke<TValue, TException>(this Func<TValue> func) where TException : Exception
		{
			if (func == null)
				throw new ArgumentNullException("func");
			try
			{
				return func();
			}
			catch (TException e)
			{
				return Result<TValue, TException>.FromException(e);
			}
		}

		public static Result<TValue, TException> TryInvoke<TValue, TException>(this Func<TValue> func, Predicate<TException> exceptionFilter) where TException : Exception
		{
			if (func == null)
				throw new ArgumentNullException("func");
			if (exceptionFilter == null)
				throw new ArgumentNullException("exceptionFilter");
			try
			{
				return func();
			}
			catch (TException e)
			{
				if (!exceptionFilter(e))
					throw;
				return Result<TValue, TException>.FromException(e);
			}
		}

		public static void TryInvoke<TValue, TException>(this Func<TValue> func, Action<TValue> ifSuccessful, Action<TException> ifFailed) where TException : Exception
		{
			if (func == null)
				throw new ArgumentNullException("func");
			if (ifSuccessful == null)
				throw new ArgumentNullException("ifSuccessful");
			if (ifFailed == null)
				throw new ArgumentNullException("ifFailed");
			TValue result;
			try
			{
				result = func();
			}
			catch (TException e)
			{
				ifFailed(e);
				return;
			}
			ifSuccessful(result);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
		public static Result<T, Exception> TryInvoke<T, TArgument>(this Func<TArgument, T> func, TArgument argument)
		{
			if (func == null)
				throw new ArgumentNullException("func");
			try
			{
				return func(argument);
			}
			catch (Exception e)
			{
				return Result<T, Exception>.FromException(e);
			}
		}

		public static Result<TValue, TException> TryInvoke<TValue, TArgument, TException>(this Func<TArgument, TValue> func, TArgument argument) where TException : Exception
		{
			if (func == null)
				throw new ArgumentNullException("func");
			try
			{
				return func(argument);
			}
			catch (TException e)
			{
				return Result<TValue, TException>.FromException(e);
			}
		}

		public static Result<TValue, TException> TryInvoke<TValue, TArgument, TException>(this Func<TArgument, TValue> func, TArgument argument, Predicate<TException> exceptionFilter) where TException : Exception
		{
			if (func == null)
				throw new ArgumentNullException("func");
			if (exceptionFilter == null)
				throw new ArgumentNullException("exceptionFilter");
			try
			{
				return func(argument);
			}
			catch (TException e)
			{
				if (!exceptionFilter(e))
					throw;
				return Result<TValue, TException>.FromException(e);
			}
		}

		public static void TryInvoke<TValue, TArgument, TException>(this Func<TArgument, TValue> func, TArgument argument, Action<TValue> ifSuccessful, Action<TException> ifFailed) where TException : Exception
		{
			if (func == null)
				throw new ArgumentNullException("func");
			if (ifSuccessful == null)
				throw new ArgumentNullException("ifSuccessful");
			if (ifFailed == null)
				throw new ArgumentNullException("ifFailed");
			TValue result;
			try
			{
				result = func(argument);
			}
			catch (TException e)
			{
				ifFailed(e);
				return;
			}
			ifSuccessful(result);
		}

		public static Result<T, AggregateException> AsResult<T>(this Task<T> task)
		{
			if (task == null)
				throw new ArgumentNullException("task");
			try
			{
				task.Wait();
				return task.Result;
			}
			catch (AggregateException e)
			{
				if (task.IsFaulted)
					return Result<T, AggregateException>.FromException(e);
				else
					throw;
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public static async Task<Result<T, Exception>> AsResultAsync<T>(this Task<T> task)
		{
			if (task == null)
				throw new ArgumentNullException("task");
			try
			{
				return await task;
			}
			catch (Exception e)
			{
				return Result<T, Exception>.FromException(e);
			}
		}

		public static long SplitTicks(this System.Diagnostics.Stopwatch stopwatch)
		{
			if (stopwatch == null)
				throw new ArgumentNullException("stopwatch");
			stopwatch.Stop();
			long et = stopwatch.ElapsedTicks;
			stopwatch.Restart();
			return et;
		}

		public static long SplitMilliseconds(this System.Diagnostics.Stopwatch stopwatch)
		{
			if (stopwatch == null)
				throw new ArgumentNullException("stopwatch");
			stopwatch.Stop();
			long et = stopwatch.ElapsedMilliseconds;
			stopwatch.Restart();
			return et;
		}

		public static TimeSpan Split(this System.Diagnostics.Stopwatch stopwatch)
		{
			if (stopwatch == null)
				throw new ArgumentNullException("stopwatch");
			stopwatch.Stop();
			TimeSpan et = stopwatch.Elapsed;
			stopwatch.Restart();
			return et;
		}
	}
}
