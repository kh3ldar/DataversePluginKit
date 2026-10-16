using System;
using KH.PluginKit;
using KH.Sample.Adapters;
using KH.Sample.Domain;

namespace KH.Sample.Plugins
{
    /// <summary>
    /// Creates a welcome-call task for a new account, then stamps the due date on the account. If the
    /// stamp fails, Compensate deletes the task. The task id travels in the run's State, not in a field.
    ///
    /// In a synchronous step this compensation is redundant: the platform's transaction rolls the task
    /// back, and Microsoft documents that a failed data operation in a synchronous plugin ends that
    /// transaction, so the delete would fail too (PluginBase traces that and rethrows the first error).
    /// It shows the mechanism for effects a transaction does not cover, such as a call to another system.
    /// </summary>
    [PluginStep("Create", "account", PluginStage.PostOperation)]
    public sealed class AccountFollowUpPlugin : PluginBase
    {
        private const string TaskKey = "followUpTaskId";
        private const int BusinessDays = 3;

        protected override void Execute(PluginRun run)
        {
            var name = run.Target?.GetAttributeValue<string>("name");
            var due = FollowUpPlanner.DueDate(DateTime.UtcNow.Date, BusinessDays);
            var accountId = run.Context.PrimaryEntityId;
            var store = new FollowUpStore(run.Service);

            run.State[TaskKey] = store.CreateTask(accountId, FollowUpPlanner.Subject(name), due);
            store.StampAccount(accountId, due);
        }

        protected override void Compensate(PluginRun run, Exception error)
        {
            object taskId;
            if (!run.State.TryGetValue(TaskKey, out taskId)) return;

            new FollowUpStore(run.Service).DeleteTask((Guid)taskId);
            run.Trace.Info("Deleted task {0} after failure", taskId);
        }
    }
}
