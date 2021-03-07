using System;

namespace Infrastructure.Handlers
{
    public class ErrorHandling
    {
        public static string errorMessage = $"Warning, somthing with the app logic isn't right, needs some verification!\nPress Enter to continue...";
        public static void CheckForUnEqualAmounts(int namesCount, int groupsCount, int linksCount)
        {
            if (namesCount != groupsCount && namesCount != linksCount)
            {
                if (namesCount > groupsCount)
                {
                    throw new Exception($"Error: Names({namesCount}) are more than the Groups({groupsCount})");
                }
                else if (namesCount < groupsCount)
                {
                    throw new Exception("Error: Groups({groupsCount}) are more than the Names({namesCount})");
                }
                else if (namesCount > linksCount)
                {
                    throw new Exception($"Error: Names({namesCount}) are more than the Links({linksCount}) Hint: It may be that not all links start with 'http' ");
                }
                else if (namesCount < linksCount)
                {
                    throw new Exception($"Error: Links({linksCount}) are more than the Names({namesCount})");
                }
                else
                {
                    throw new Exception(errorMessage);
                }
            }
        }
        public static void CheckForUnEqualAmounts(int namesCount, int linksCount)
        {
            if (namesCount != linksCount)
            {
                if (namesCount > linksCount)
                {
                    throw new Exception($"Error: Names({namesCount}) are more than the Links({linksCount}) Hint: It may be that not all links start with 'http' ");
                }
                else if (namesCount < linksCount)
                {
                    throw new Exception($"Error: Links({linksCount}) are more than the Names({namesCount})");
                }
                else
                {
                    throw new Exception(errorMessage);
                }
            }
        }
    }
}