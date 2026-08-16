using System.Globalization;
using System.Text.RegularExpressions;

namespace lotus_blue.MiddleWear
{
    public class ArabicTimeFormatMiddleware
    {
        private readonly RequestDelegate _next;

        public ArabicTimeFormatMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Replace the response stream with a memory stream
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                await _next(context); // Process the request

                // Check if the content type is text-based (e.g., HTML, JSON)
                var contentType = context.Response.ContentType;
                if (!string.IsNullOrEmpty(contentType) && contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
                {
                    // Read the response body
                    responseBody.Seek(0, SeekOrigin.Begin);
                    var responseText = await new StreamReader(responseBody).ReadToEndAsync();

                    // Regex pattern to match date-time strings with AM/PM
                    var dateTimePattern = @"\b\d{1,2}:\d{2}\s?(AM|PM)\b";

                    // Replace AM/PM with ص/م only in date-time strings
                    responseText = Regex.Replace(responseText, dateTimePattern, match =>
                    {
                        var timeString = match.Value;
                        if (timeString.Contains("AM"))
                        {
                            return timeString.Replace("AM", "ص");
                        }
                        else if (timeString.Contains("PM"))
                        {
                            return timeString.Replace("PM", "م");
                        }
                        return timeString;
                    }, RegexOptions.IgnoreCase);

                    // Reset the response stream and write the modified content
                    context.Response.Body = originalBodyStream;
                    context.Response.ContentLength = responseText.Length;
                    await context.Response.WriteAsync(responseText);
                }
                else
                {
                    // If the content type is not text, simply copy the response body back to the original stream
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
        }
    }
}
