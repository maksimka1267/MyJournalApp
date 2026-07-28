namespace MyJournalApp.Result
{
    public class ServiceResult<T> : IServiceResult
    {
        public T? Data { get; init; }

        public static ServiceResult<T> Ok(T data, string message = "")
            => new()
            {
                Success = true,
                Data = data,
                Message = message,
                StatusCode = 200
            };

        public new static ServiceResult<T> Fail(string message, int statusCode = 400)
            => new()
            {
                Success = false,
                Message = message,
                StatusCode = statusCode
            };
    }
}
