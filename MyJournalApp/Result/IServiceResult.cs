namespace MyJournalApp.Result
{
    public class IServiceResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int StatusCode { get; init; } = 200;

        public static IServiceResult Ok(string message = "")
            => new()
            {
                Success = true,
                Message = message,
                StatusCode = 200
            };

        public static IServiceResult Fail(string message, int statusCode = 400)
            => new()
            {
                Success = false,
                Message = message,
                StatusCode = statusCode
            };
    }
}
