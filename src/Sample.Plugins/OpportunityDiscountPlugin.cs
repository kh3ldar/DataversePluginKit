using System;
using KH.PluginKit;
using KH.Sample.Adapters;
using KH.Sample.Domain;
using Microsoft.Xrm.Sdk;

namespace KH.Sample.Plugins
{
    /// <summary>
    /// Guards the discount on an opportunity. Authorise: a discount above the standard limit needs an
    /// approver. Validate: a discount beyond the hard limit is refused. Execute: stamps the tier on the row.
    /// Runs before the write, so changing the Target here is saved with the row.
    /// </summary>
    [PluginStep("Create", "opportunity", PluginStage.PreOperation)]
    [PluginStep("Update", "opportunity", PluginStage.PreOperation,
        FilteringAttributes = "discountpercentage", PreImage = "discountpercentage")]
    public sealed class OpportunityDiscountPlugin : PluginBase
    {
        public const string TierColumn = "kh_discounttier";

        private static readonly DiscountPolicy Policy = new DiscountPolicy();

        protected override void Authorize(PluginRun run)
        {
            var percent = ChangedPercent(run);
            if (percent == null || Policy.Classify(percent.Value) != DiscountTier.NeedsApproval) return;

            var directory = new ApproverDirectory(run.Service);
            if (!directory.IsApprover(run.Context.InitiatingUserId))
                throw new InvalidPluginExecutionException(
                    "A discount above " + Policy.StandardLimit + "% needs a member of " + ApproverDirectory.TeamName + ".");
        }

        protected override void Validate(PluginRun run)
        {
            var percent = ChangedPercent(run);
            if (percent != null && Policy.Classify(percent.Value) == DiscountTier.Rejected)
                throw new InvalidPluginExecutionException(
                    "A discount must be between 0% and " + Policy.HardLimit + "%.");
        }

        protected override void Execute(PluginRun run)
        {
            var percent = ChangedPercent(run);
            if (percent == null) return;

            var tier = Policy.Classify(percent.Value);
            run.Target[TierColumn] = tier.ToString();
            run.Trace.Info("Discount {0}% classed as {1}", percent.Value, tier);
        }

        /// <summary>The new discount, or null when this operation does not change it.</summary>
        private static decimal? ChangedPercent(PluginRun run)
        {
            var target = run.Target;
            if (target == null || !target.Contains("discountpercentage")) return null;

            var now = target.GetAttributeValue<decimal?>("discountpercentage");
            var before = run.PreImage?.GetAttributeValue<decimal?>("discountpercentage");
            return now == before ? null : now;
        }
    }
}
