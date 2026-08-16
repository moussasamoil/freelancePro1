using System;

namespace lotus_blue.Services
{
    public class GetCurrentTimeInIstanbul
    {
        // Function to get the current time in Istanbul time zone
        public DateTime GetIstanbulTimeWithOffset()
        {
            // Define the time zone for Istanbul
            TimeZoneInfo istanbulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

            // Get the current time in Istanbul
            DateTime utcNow = DateTime.UtcNow;
            DateTime istanbulTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, istanbulTimeZone);

            // Return the Istanbul time
            return istanbulTime;
        }
    }
}
