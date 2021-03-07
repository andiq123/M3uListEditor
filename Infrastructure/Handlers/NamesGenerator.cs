using System;

namespace Infrastructure.Handlers
{
    public class NamesGenerator
    {
        public static string GenerateNameByDateAndHour()
        {
            var date = DateTime.Now;
            return $"{date.Year}-{date.Month}_{date.Hour}-{date.Minute}-{date.Second}";
        }
    }
}