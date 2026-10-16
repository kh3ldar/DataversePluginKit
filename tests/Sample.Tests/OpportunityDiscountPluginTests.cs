using System;
using KH.PluginKit.Testing;
using KH.Sample.Adapters;
using KH.Sample.Plugins;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace KH.Sample.Tests
{
    public class OpportunityDiscountPluginTests
    {
        private static FakeServiceProvider Update(decimal? before, decimal? after, bool userIsApprover = false)
        {
            var target = new Entity("opportunity", Guid.NewGuid());
            if (after != null) target["discountpercentage"] = after.Value;

            var context = FakePluginContext.For("Update", "opportunity", 20, target);
            var image = new Entity("opportunity", target.Id);
            if (before != null) image["discountpercentage"] = before.Value;
            context.PreEntityImages["PreImage"] = image;

            var provider = new FakeServiceProvider(context);
            provider.Service.OnRetrieveMultiple = _ =>
            {
                var rows = new EntityCollection();
                if (userIsApprover) rows.Entities.Add(new Entity("teammembership"));
                return rows;
            };
            return provider;
        }

        [Fact]
        public void A_standard_discount_is_stamped_without_asking_the_directory()
        {
            var provider = Update(0m, 5m);
            new OpportunityDiscountPlugin().Execute(provider);

            Assert.Equal("Standard", ((Entity)provider.Context.InputParameters["Target"])[OpportunityDiscountPlugin.TierColumn]);
            Assert.Empty(provider.Service.Queries);
        }

        [Fact]
        public void A_larger_discount_needs_an_approver()
        {
            var provider = Update(5m, 20m);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new OpportunityDiscountPlugin().Execute(provider));

            Assert.Contains("Discount Approvers", ex.Message);
            Assert.False(((Entity)provider.Context.InputParameters["Target"]).Contains(OpportunityDiscountPlugin.TierColumn));
        }

        [Fact]
        public void An_approver_may_give_the_larger_discount()
        {
            var provider = Update(5m, 20m, userIsApprover: true);
            new OpportunityDiscountPlugin().Execute(provider);

            Assert.Equal("NeedsApproval", ((Entity)provider.Context.InputParameters["Target"])[OpportunityDiscountPlugin.TierColumn]);
        }

        [Fact]
        public void The_approver_check_uses_the_user_who_started_the_operation()
        {
            var provider = Update(5m, 20m, userIsApprover: true);
            new OpportunityDiscountPlugin().Execute(provider);

            var query = (QueryExpression)Assert.Single(provider.Service.Queries);
            var condition = Assert.Single(query.Criteria.Conditions);
            Assert.Equal(provider.Context.InitiatingUserId, condition.Values[0]);
        }

        [Fact]
        public void A_discount_beyond_the_hard_limit_is_rejected_even_for_an_approver()
        {
            var provider = Update(5m, 45m, userIsApprover: true);
            Assert.Throws<InvalidPluginExecutionException>(() => new OpportunityDiscountPlugin().Execute(provider));
        }

        [Fact]
        public void An_update_that_leaves_the_discount_alone_does_nothing()
        {
            var provider = Update(40m, 40m);
            new OpportunityDiscountPlugin().Execute(provider);

            Assert.False(((Entity)provider.Context.InputParameters["Target"]).Contains(OpportunityDiscountPlugin.TierColumn));
        }

        [Fact]
        public void A_create_has_no_pre_image_and_still_works()
        {
            var target = new Entity("opportunity", Guid.NewGuid());
            target["discountpercentage"] = 8m;
            var provider = new FakeServiceProvider(FakePluginContext.For("Create", "opportunity", 20, target));

            new OpportunityDiscountPlugin().Execute(provider);

            Assert.Equal("Standard", target[OpportunityDiscountPlugin.TierColumn]);
        }

        [Fact]
        public void The_approver_query_targets_the_approver_team()
        {
            var service = new FakeOrganizationService
            {
                OnRetrieveMultiple = _ => new EntityCollection(),
            };
            Assert.False(new ApproverDirectory(service).IsApprover(Guid.NewGuid()));

            var query = (QueryExpression)Assert.Single(service.Queries);
            Assert.Equal("teammembership", query.EntityName);
            Assert.Equal(ApproverDirectory.TeamName, Assert.Single(query.LinkEntities).LinkCriteria.Conditions[0].Values[0]);
        }
    }
}
