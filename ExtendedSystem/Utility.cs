using System;
using System.Threading.Tasks;

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
