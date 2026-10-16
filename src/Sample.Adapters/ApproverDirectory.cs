using System;
using KH.Sample.Domain;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace KH.Sample.Adapters
{
    /// <summary>Approvers are the members of one team, found with a QueryExpression on team membership.</summary>
    public sealed class ApproverDirectory : IApproverDirectory
    {
        public const string TeamName = "Discount Approvers";

        private readonly IOrganizationService _service;

        public ApproverDirectory(IOrganizationService service)
        {
            _service = service;
        }

        public bool IsApprover(Guid userId)
        {
            var query = new QueryExpression("teammembership")
            {
                ColumnSet = new ColumnSet("systemuserid"),
                TopCount = 1,
            };
            query.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);
            var team = query.AddLink("team", "teamid", "teamid");
            team.LinkCriteria.AddCondition("name", ConditionOperator.Equal, TeamName);

            return _service.RetrieveMultiple(query).Entities.Count > 0;
        }
    }
}
