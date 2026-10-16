namespace KH.Sample.Domain
{
    public enum DiscountTier
    {
        None,
        Standard,
        NeedsApproval,
        Rejected,
    }

    /// <summary>
    /// The discount rule, as plain code. Up to the standard limit anyone may give the discount; up to
    /// the hard limit an approver must; beyond it nobody may. No SDK types, so it tests without a fake.
    /// </summary>
    public sealed class DiscountPolicy
    {
        public decimal StandardLimit { get; }
        public decimal HardLimit { get; }

        public DiscountPolicy(decimal standardLimit = 10m, decimal hardLimit = 30m)
        {
            StandardLimit = standardLimit;
            HardLimit = hardLimit;
        }

        public DiscountTier Classify(decimal percent)
        {
            if (percent < 0m || percent > HardLimit) return DiscountTier.Rejected;
            if (percent == 0m) return DiscountTier.None;
            return percent <= StandardLimit ? DiscountTier.Standard : DiscountTier.NeedsApproval;
        }
    }
}
