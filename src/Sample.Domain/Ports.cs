using System;

namespace KH.Sample.Domain
{
    /// <summary>Who may approve a discount above the standard limit. Implemented in the adapters.</summary>
    public interface IApproverDirectory
    {
        bool IsApprover(Guid userId);
    }

    /// <summary>Where follow-up work for an account is written. Implemented in the adapters.</summary>
    public interface IFollowUpStore
    {
        Guid CreateTask(Guid accountId, string subject, DateTime due);

        void DeleteTask(Guid taskId);

        void StampAccount(Guid accountId, DateTime due);
    }
}
