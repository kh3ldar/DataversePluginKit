using System;
using KH.Sample.Domain;
using Xunit;

namespace KH.Sample.Tests
{
    public class DomainTests
    {
        [Theory]
        [InlineData(0, DiscountTier.None)]
        [InlineData(10, DiscountTier.Standard)]
        [InlineData(10.5, DiscountTier.NeedsApproval)]
        [InlineData(30, DiscountTier.NeedsApproval)]
        [InlineData(30.01, DiscountTier.Rejected)]
        [InlineData(-1, DiscountTier.Rejected)]
        public void Discounts_are_classified_by_limit(double percent, DiscountTier expected)
        {
            Assert.Equal(expected, new DiscountPolicy().Classify((decimal)percent));
        }

        [Fact]
        public void Limits_are_configurable()
        {
            Assert.Equal(DiscountTier.NeedsApproval, new DiscountPolicy(5m, 50m).Classify(6m));
        }

        [Fact]
        public void Due_date_skips_weekends()
        {
            var thursday = new DateTime(2026, 1, 1);
            Assert.Equal(DayOfWeek.Thursday, thursday.DayOfWeek);
            Assert.Equal(new DateTime(2026, 1, 6), FollowUpPlanner.DueDate(thursday, 3));
        }

        [Fact]
        public void Zero_business_days_is_the_same_day()
        {
            var day = new DateTime(2026, 1, 3);
            Assert.Equal(day, FollowUpPlanner.DueDate(day, 0));
        }

        [Theory]
        [InlineData("  Contoso  ", "Welcome call: Contoso")]
        [InlineData(null, "Welcome call: new account")]
        public void Subject_names_the_account(string name, string expected)
        {
            Assert.Equal(expected, FollowUpPlanner.Subject(name));
        }
    }
}
