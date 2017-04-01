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
		private TValue _value;
		private TException _exception;

		internal Result()
		{
		}

		public Result(TValue value)
		{
			this.Success = true;
			this._value = value;
		}

		public static implicit operator Result<TValue, TException>(TValue value)
		{
			return new Result<TValue, TException>(value);
		}

		internal static Result<TValue, TException> FromException(TException e)
		{
			var r = new Result<TValue, TException>()
			{
				_exception = e
			};
			return r;
		}

		public TValue Assert()
		{
			if (this.Success)
				return this._value;
			else
				throw this._exception;
		}

		public TException GetException()
		{
			return this._exception;
		}

		public bool TryGet(out TValue result)
		{
			if (this.Success)
				result = this._value;
			else
				result = default(TValue);
			return this.Success;
		}

		public bool TryGet(out TValue result, TValue @default)
		{
			if (this.Success)
				result = this._value;
			else
				result = @default;
			return this.Success;
		}

		public bool Equals(Result<TValue, TException> other)
		{
			if (other == null)
				return false;
			if (this.Success != other.Success)
				return false;
			if (this.Success)
				return this._value.Equals(other._value);
			else
				return this._exception.Equals(other._exception);
		}

		public override bool Equals(object obj)
		{
			var other = obj as Result<TValue, TException>;
			return this.Equals(other);
		}

		public override int GetHashCode()
		{
			return this.Success ? this._value.GetHashCode() : this._exception.GetHashCode();
		}

		public override string ToString()
		{
			return this.Success ? this._value.ToString() : this._exception.ToString();
		}

		public Result<TValue, TOtherException> CastException<TOtherException>() where TOtherException : Exception
		{
			var other = (TOtherException)((Exception)this._exception);
			return this.Success ? new Result<TValue, TOtherException>(this._value) : Result<TValue, TOtherException>.FromException(other);
		}
	}

}
