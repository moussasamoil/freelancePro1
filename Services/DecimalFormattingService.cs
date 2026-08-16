namespace lotus_blue.Services
{
    public  class DecimalFormattingService
    {
        public static string FormatDecimal(decimal value)
        {
            // If the decimal part is zero, format without decimal places
            if (value == Math.Floor(value))
            {
                return value.ToString("N0");
            }
            else
            {
                // Otherwise, format with two decimal places
                return value.ToString("N2");
            }
        }

        public string DecimalFormat(decimal value)
        {
            // If the decimal part is zero, format without decimal places
            if (value == Math.Floor(value))
            {
                return value.ToString("N0");
            }
            else
            {
                // Otherwise, format with two decimal places
                return value.ToString("N2");
            }
        }

    }

   


}
