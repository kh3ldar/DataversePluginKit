using System;

namespace KH.Sample.Domain
{
    /// <summary>Works out when the follow-up for a new account is due.</summary>
    public static class FollowUpPlanner
    {
        public static string Subject(string accountName)
        {
            var name = string.IsNullOrWhiteSpace(accountName) ? "new account" : accountName.Trim();
            return "Welcome call: " + name;
        }

        /// <summary>The date that is <paramref name="businessDays"/> weekdays after <paramref name="from"/>.</summary>
        public static DateTime DueDate(DateTime from, int businessDays)
        {
            var date = from;
            var left = businessDays;
            while (left > 0)
            {
                date = date.AddDays(1);
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) left--;
            }
            return date;
        }
    }
}
