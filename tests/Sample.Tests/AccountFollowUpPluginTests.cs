using System;
using KH.PluginKit.Testing;
using KH.Sample.Adapters;
using KH.Sample.Plugins;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace KH.Sample.Tests
{
    public class AccountFollowUpPluginTests
    {
        private static FakeServiceProvider NewAccount()
        {
            var target = new Entity("account", Guid.NewGuid());
            target["name"] = "Contoso";
            return new FakeServiceProvider(FakePluginContext.For("Create", "account", 40, target));
        }

        [Fact]
        public void Creates_a_task_regarding_the_account_and_stamps_the_due_date()
        {
            var provider = NewAccount();
            new AccountFollowUpPlugin().Execute(provider);

            var task = Assert.Single(provider.Service.Created);
            Assert.Equal("Welcome call: Contoso", task["subject"]);
            Assert.Equal(provider.Context.PrimaryEntityId, ((EntityReference)task["regardingobjectid"]).Id);

            var account = Assert.Single(provider.Service.Updated);
            Assert.Equal(task["scheduledend"], account[FollowUpStore.DueColumn]);
            Assert.Empty(provider.Service.Deleted);
        }

        [Fact]
        public void When_the_stamp_fails_the_task_is_deleted_and_the_error_propagates()
        {
            var provider = NewAccount();
            provider.Service.UpdateFault = _ => new InvalidPluginExecutionException("stamp failed");

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => new AccountFollowUpPlugin().Execute(provider));

            Assert.Equal("stamp failed", ex.Message);
            var deleted = Assert.Single(provider.Service.Deleted);
            Assert.Equal("task", deleted.LogicalName);
            Assert.Equal(provider.Service.Created[0].Id, deleted.Id);
        }

        [Fact]
        public void When_creating_the_task_fails_there_is_nothing_to_delete()
        {
            var provider = NewAccount();
            provider.Service.CreateFault = new InvalidPluginExecutionException("create failed");

            Assert.Throws<InvalidPluginExecutionException>(() => new AccountFollowUpPlugin().Execute(provider));
            Assert.Empty(provider.Service.Deleted);
        }
    }
}
