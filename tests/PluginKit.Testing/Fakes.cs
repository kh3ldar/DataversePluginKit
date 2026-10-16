using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace KH.PluginKit.Testing
{
    /// <summary>Resolves the three services a plugin asks for. Hand-rolled; no mocking library.</summary>
    public sealed class FakeServiceProvider : IServiceProvider
    {
        public FakePluginContext Context { get; }
        public FakeTracingService Tracing { get; } = new FakeTracingService();
        public FakeOrganizationService Service { get; } = new FakeOrganizationService();

        public FakeServiceProvider(FakePluginContext context)
        {
            Context = context;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IPluginExecutionContext)) return Context;
            if (serviceType == typeof(ITracingService)) return Tracing;
            if (serviceType == typeof(IOrganizationServiceFactory)) return new FakeServiceFactory(this);
            return null;
        }

        private sealed class FakeServiceFactory : IOrganizationServiceFactory
        {
            private readonly FakeServiceProvider _owner;

            public FakeServiceFactory(FakeServiceProvider owner)
            {
                _owner = owner;
            }

            public IOrganizationService CreateOrganizationService(Guid? userId)
            {
                _owner.Service.CreatedFor = userId;
                return _owner.Service;
            }
        }
    }

    public sealed class FakeTracingService : ITracingService
    {
        public List<string> Lines { get; } = new List<string>();

        public void Trace(string format, params object[] args)
        {
            Lines.Add(args == null || args.Length == 0 ? format : string.Format(format, args));
        }
    }

    /// <summary>A settable execution context. Only the members the kit reads matter; the rest are inert.</summary>
    public sealed class FakePluginContext : IPluginExecutionContext
    {
        public string MessageName { get; set; }
        public string PrimaryEntityName { get; set; }
        public Guid PrimaryEntityId { get; set; }
        public int Stage { get; set; }
        public int Mode { get; set; }
        public int Depth { get; set; } = 1;
        public Guid UserId { get; set; } = Guid.NewGuid();
        public Guid InitiatingUserId { get; set; } = Guid.NewGuid();
        public Guid CorrelationId { get; set; } = Guid.NewGuid();

        public ParameterCollection InputParameters { get; } = new ParameterCollection();
        public ParameterCollection OutputParameters { get; } = new ParameterCollection();
        public ParameterCollection SharedVariables { get; } = new ParameterCollection();
        public EntityImageCollection PreEntityImages { get; } = new EntityImageCollection();
        public EntityImageCollection PostEntityImages { get; } = new EntityImageCollection();

        public IPluginExecutionContext ParentContext => null;
        public int IsolationMode => 2;
        public bool IsExecutingOffline => false;
        public bool IsOfflinePlayback => false;
        public bool IsInTransaction => true;
        public Guid OperationId => Guid.Empty;
        public DateTime OperationCreatedOn => DateTime.UtcNow;
        public Guid BusinessUnitId => Guid.Empty;
        public Guid OrganizationId => Guid.Empty;
        public string OrganizationName => "fake";
        public Guid? RequestId => null;
        public string SecondaryEntityName => "none";
        public EntityReference OwningExtension => null;

        /// <summary>A context for <paramref name="message"/> on <paramref name="table"/> that carries a Target.</summary>
        public static FakePluginContext For(string message, string table, int stage, Entity target = null, int mode = 0)
        {
            var context = new FakePluginContext
            {
                MessageName = message,
                PrimaryEntityName = table,
                Stage = stage,
                Mode = mode,
            };
            if (target != null)
            {
                context.InputParameters["Target"] = target;
                context.PrimaryEntityId = target.Id;
            }
            return context;
        }
    }

    /// <summary>Records calls and lets a test inject answers and faults.</summary>
    public sealed class FakeOrganizationService : IOrganizationService
    {
        public Guid? CreatedFor { get; set; }
        public List<Entity> Created { get; } = new List<Entity>();
        public List<Entity> Updated { get; } = new List<Entity>();
        public List<EntityReference> Deleted { get; } = new List<EntityReference>();
        public List<QueryBase> Queries { get; } = new List<QueryBase>();

        /// <summary>Answers RetrieveMultiple. Default: no rows.</summary>
        public Func<QueryBase, EntityCollection> OnRetrieveMultiple { get; set; }

        /// <summary>When set, Update throws what this returns (or does nothing when it returns null).</summary>
        public Func<Entity, Exception> UpdateFault { get; set; }

        /// <summary>When set, Create throws it.</summary>
        public Exception CreateFault { get; set; }

        public Guid Create(Entity entity)
        {
            if (CreateFault != null) throw CreateFault;
            var id = Guid.NewGuid();
            Created.Add(entity);
            entity.Id = id;
            return id;
        }

        public void Update(Entity entity)
        {
            var fault = UpdateFault?.Invoke(entity);
            if (fault != null) throw fault;
            Updated.Add(entity);
        }

        public void Delete(string entityName, Guid id) => Deleted.Add(new EntityReference(entityName, id));

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            Queries.Add(query);
            return OnRetrieveMultiple?.Invoke(query) ?? new EntityCollection();
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => new Entity(entityName, id);

        public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
            => throw new NotSupportedException();

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
            => throw new NotSupportedException();
    }
}
