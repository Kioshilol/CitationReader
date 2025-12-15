using System;
using System.Text.Json.Serialization;

namespace CitationReader.Models.Base
{
    public abstract class BaseResponse
    {
        [JsonPropertyName("reason")]
        public int Reason { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("stackTrace")]
        public string? StackTrace { get; set; }

        [JsonIgnore]
        public bool IsSuccess => Reason == 0;

        [JsonIgnore]
        public bool IsFailure => !IsSuccess;
    }

    public class BaseResponse<T> : BaseResponse
    {
        [JsonPropertyName("result")]
        public T? Result { get; set; }

        public static BaseResponse<T> Success(T result, string? message = null)
        {
            return new BaseResponse<T>
            {
                Reason = 0,
                Message = message,
                Result = result
            };
        }

        public static BaseResponse<T> Failure(int reason, string message, string? stackTrace = null)
        {
            return new BaseResponse<T>
            {
                Reason = reason,
                Message = message,
                StackTrace = stackTrace,
                Result = default(T)
            };
        }

        public static BaseResponse<T> Failure(Exception exception, int reason = -1)
        {
            return new BaseResponse<T>
            {
                Reason = reason,
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                Result = default(T)
            };
        }
    }
}
