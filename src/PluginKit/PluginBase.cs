using System;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace KH.PluginKit
{
    /// <summary>
    /// Base class for every plugin in the kit. The platform calls <see cref="Execute(IServiceProvider)"/>,
    /// which is not virtual: it builds a <see cref="PluginRun"/>, refuses a step the class does not declare,
    /// then calls the hooks in one fixed order (authorise, validate, execute, and compensate if execute
    /// threw). Only <see cref="Execute(PluginRun)"/> must be overridden.
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var run = PluginRun.From(serviceProvider);
            EnsureDeclared(run);
            run.Trace.Info("{0} start, depth {1}", GetType().Name, run.Context.Depth);

            // The guards throw to refuse. Nothing has been written yet, so a refusal needs no clean-up.
            Authorize(run);
            Validate(run);

            ExecuteOrCompensate(run);
        }

        /// <summary>The steps declared on a plugin class with <see cref="PluginStepAttribute"/>.</summary>
        public static PluginStepAttribute[] StepsOf(Type pluginType)
        {
            return pluginType.GetCustomAttributes(typeof(PluginStepAttribute), false)
                .Cast<PluginStepAttribute>().ToArray();
        }

        /// <summary>Refuse a caller who may not do this, by throwing <see cref="InvalidPluginExecutionException"/>. Default: allow.</summary>
        protected virtual void Authorize(PluginRun run) { }

        /// <summary>Refuse a request that breaks a rule, by throwing <see cref="InvalidPluginExecutionException"/>. Default: accept.</summary>
        protected virtual void Validate(PluginRun run) { }

        /// <summary>The plugin's work. Runs only after both guards have passed.</summary>
        protected abstract void Execute(PluginRun run);

        /// <summary>
        /// Clean-up for side effects of <see cref="Execute(PluginRun)"/> when it throws. Called before the
        /// exception leaves <see cref="Execute(IServiceProvider)"/>. Never called for a guard refusal.
        /// </summary>
        protected virtual void Compensate(PluginRun run, Exception error) { }

        private void EnsureDeclared(PluginRun run)
        {
            var declared = StepsOf(GetType());
            if (declared.Length == 0)
                throw new InvalidOperationException(GetType().FullName + " declares no [PluginStep].");

            var context = run.Context;
            foreach (var step in declared)
            {
                if (step.Matches(context)) return;
            }

            throw new InvalidPluginExecutionException(string.Format(
                "{0} is not registered the way it declares. It ran for {1} on {2}, stage {3}, mode {4}; it declares {5}.",
                GetType().Name, context.MessageName, context.PrimaryEntityName, context.Stage, context.Mode,
                string.Join(" or ", declared.Select(step => step.ToString()))));
        }

        private void ExecuteOrCompensate(PluginRun run)
        {
            try
            {
                Execute(run);
            }
            catch (Exception failure)
            {
                run.Trace.Warn("{0} failed: {1}", GetType().Name, failure.Message);
                TryCompensate(run, failure);
                throw;
            }
        }

        private void TryCompensate(PluginRun run, Exception failure)
        {
            try
            {
                Compensate(run, failure);
            }
            catch (Exception cleanupFailure)
            {
                // The first exception says what went wrong, so it is the one the caller gets. This one is traced.
                run.Trace.Warn("Compensation failed too: {0}", cleanupFailure.Message);
            }
        }
    }
}
