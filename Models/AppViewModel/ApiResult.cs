public class ApiResult
{
    public int StatusCode { get; set; }
    public string Message { get; set; }

    public ApiResult(int statusCode, string message = "")
    {
        StatusCode = statusCode;
        Message = string.IsNullOrWhiteSpace(message) ? GetDefaultMessage(statusCode) : message;
    }

    public string GetDefaultMessage(int code)
    {
        return code switch
        {
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            401 => "Unauthorized",
            _ => "Error"
        };
    }
}
