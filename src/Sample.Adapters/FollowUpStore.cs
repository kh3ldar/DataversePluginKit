using System;
using KH.Sample.Domain;
using Microsoft.Xrm.Sdk;

namespace KH.Sample.Adapters
{
    /// <summary>Writes follow-up work with the organization service: a task, and a stamp on the account.</summary>
    public sealed class FollowUpStore : IFollowUpStore
    {
        /// <summary>Custom column on account. A custom table or column would be created with the kh_ prefix.</summary>
        public const string DueColumn = "kh_followupdue";

        private readonly IOrganizationService _service;

        public FollowUpStore(IOrganizationService service)
        {
            _service = service;
        }

        public Guid CreateTask(Guid accountId, string subject, DateTime due)
        {
            var task = new Entity("task");
            task["subject"] = subject;
            task["scheduledend"] = due;
            task["regardingobjectid"] = new EntityReference("account", accountId);
            return _service.Create(task);
        }

        public void DeleteTask(Guid taskId)
        {
            _service.Delete("task", taskId);
        }

        public void StampAccount(Guid accountId, DateTime due)
        {
            var account = new Entity("account", accountId);
            account[DueColumn] = due;
            _service.Update(account);
        }
    }
}
