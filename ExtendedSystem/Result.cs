using System;

namespace ExtendedSystem
{
	public interface IResult<out TValue, out TException> where TException : Exception
	{
		bool Success
		{
			get;
		}

		TValue Assert();

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
		TException GetException();
	}

	/// <summary>
	/// Encloses the result of a function call. Picture it as an object extension of HRESULT.
	/// </summary>
	/// <typeparam name="TValue"></typeparam>
	/// <typeparam name="TException"></typeparam>
	public class Result<TValue, TException> : IResult<TValue, TException> where TException : Exception
	{
		public bool Success
		{
			get;
		}
		private TValue value;
		private TException exception;

		internal Result()
		{
		}

		public Result(TValue value)
		{
			Success = true;
			this.value = value;
		}

		public static implicit operator Result<TValue, TException>(TValue value)
		{
			return new Result<TValue, TException>(value);
		}

		internal static Result<TValue, TException> FromException(TException e)
		{
			var r = new Result<TValue, TException>();
			r.exception = e;
			return r;
		}

		public TValue Assert()
		{
			if (Success)
				return value;
			else
				throw exception;
		}

		public TException GetException()
		{
			return exception;
		}

		public bool TryGet(out TValue result)
		{
			if (Success)
				result = this.value;
			else
				result = default(TValue);
			return Success;
		}

		public bool TryGet(out TValue result, TValue @default)
		{
			if (Success)
				result = this.value;
			else
				result = @default;
			return Success;
		}

		public bool Equals(Result<TValue, TException> other)
		{
			if (other == null)
				return false;
			if (this.Success != other.Success)
				return false;
			if (Success)
				return this.value.Equals(other.value);
			else
				return this.exception.Equals(other.exception);
		}

		public override bool Equals(object obj)
		{
			Result<TValue, TException> other = obj as Result<TValue, TException>;
			return this.Equals(other);
		}

		public override int GetHashCode()
		{
			return Success ? value.GetHashCode() : exception.GetHashCode();
		}

		public override string ToString()
		{
			return Success ? value.ToString() : exception.ToString();
		}

		public Result<TValue, TOtherException> CastException<TOtherException>() where TOtherException : Exception
		{
			TOtherException other = (TOtherException)((Exception)exception);
			return Success ? new Result<TValue, TOtherException>(value) : Result<TValue, TOtherException>.FromException(other);
		}
	}

}
