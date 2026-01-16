using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalisationERP.Application.Interfaces
{
    /// <summary>
    /// Interface pour les résultats d'opération
    /// </summary>
    public interface IResult<T>
    {
        bool IsSuccess { get; }
        T? Data { get; }
        string Message { get; }
    }

    /// <summary>
    /// Implémentation générique des résultats
    /// </summary>
    public class Result<T> : IResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public string Message { get; private set; } = "";
        public List<string> Errors { get; private set; } = new();

        private Result() { }

        public static Result<T> Ok(T data, string message = "Success")
            => new Result<T> { IsSuccess = true, Data = data, Message = message };

        public static Result<T> Fail(string message, List<string>? errors = null)
            => new Result<T> { IsSuccess = false, Data = default, Message = message, Errors = errors ?? new() };

        // Legacy support for old method names
        public static Result<T> Success(T data, string message = "Success")
            => Ok(data, message);

        public static Result<T> Failure(string message)
            => Fail(message);
    }
}
