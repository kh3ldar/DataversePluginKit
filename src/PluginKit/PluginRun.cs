using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace KH.PluginKit
{
    /// <summary>
    /// One execution of a plugin: the execution context, an organization service and a correlation-tagged
    /// trace, resolved from the service provider before any hook runs, plus per-execution
    /// <see cref="State"/>. A new instance is made for every call to <see cref="PluginBase"/>.
    /// </summary>
    public sealed class PluginRun
    {
        public IPluginExecutionContext Context { get; }
        public IOrganizationService Service { get; }
        public PluginTrace Trace { get; }

        /// <summary>
        /// Values that one hook hands to a later one, such as an id created in Execute that Compensate
        /// needs. They live here, not in plugin fields: the platform caches a plugin instance and reuses it,
        /// so a field is shared between executions.
        /// </summary>
        public IDictionary<string, object> State { get; } = new Dictionary<string, object>();

        /// <summary>The Target input parameter if it is an Entity (as on Create and Update), else null.</summary>
        public Entity Target
        {
            get
            {
                object value;
                return Context.InputParameters.TryGetValue("Target", out value) ? value as Entity : null;
            }
        }

        /// <summary>The image registered under <see cref="PluginStepAttribute.PreImageAlias"/>, or null.</summary>
        public Entity PreImage
        {
            get
            {
                Entity image;
                return Context.PreEntityImages.TryGetValue(PluginStepAttribute.PreImageAlias, out image) ? image : null;
            }
        }

        public PluginRun(IPluginExecutionContext context, IOrganizationService service, PluginTrace trace)
        {
            Context = context;
            Service = service;
            Trace = trace;
        }

        /// <summary>Resolves what a run needs from the provider the platform passes to IPlugin.Execute.</summary>
        public static PluginRun From(IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            var context = Require<IPluginExecutionContext>(provider);
            var factory = Require<IOrganizationServiceFactory>(provider);
            var trace = new PluginTrace(Require<ITracingService>(provider), context.CorrelationId);

            // UserId is the user the step is set to run as (the calling user unless the registration names
            // another). Passing null instead would act as SYSTEM; InitiatingUserId is who started the operation.
            return new PluginRun(context, factory.CreateOrganizationService(context.UserId), trace);
        }

        private static T Require<T>(IServiceProvider provider) where T : class
        {
            var service = provider.GetService(typeof(T)) as T;
            if (service == null)
                throw new InvalidOperationException("The service provider returned no " + typeof(T).Name + ".");
            return service;
        }
    }
}
