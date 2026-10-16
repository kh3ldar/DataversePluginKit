using System;

namespace KH.PluginKit
{
    /// <summary>Pipeline stage of a step. Stage 30 (main operation) is not registrable.</summary>
    public enum PluginStage
    {
        PreValidation = 10,
        PreOperation = 20,
        PostOperation = 40,
    }

    public enum PluginMode
    {
        Synchronous = 0,
        Asynchronous = 1,
    }

    /// <summary>
    /// Declares one Dataverse step a plugin is registered on. Put one per step on the plugin class.
    /// The declaration is the source of truth for two things: the runtime check in
    /// <see cref="PluginBase"/> and the generated step table in STEPS.md.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class PluginStepAttribute : Attribute
    {
        public const string PreImageAlias = "PreImage";
        public const string PostImageAlias = "PostImage";

        public string Message { get; }
        public string Table { get; }
        public PluginStage Stage { get; }

        public PluginMode Mode { get; set; } = PluginMode.Synchronous;

        /// <summary>Comma-separated column logical names for Update steps. Null or empty means every column.</summary>
        public string FilteringAttributes { get; set; }

        /// <summary>Comma-separated columns of the pre-image (alias "PreImage"). Null means no pre-image.</summary>
        public string PreImage { get; set; }

        /// <summary>Comma-separated columns of the post-image (alias "PostImage"). Null means no post-image.</summary>
        public string PostImage { get; set; }

        public PluginStepAttribute(string message, string table, PluginStage stage)
        {
            Message = message;
            Table = table;
            Stage = stage;
        }

        /// <summary>
        /// Whether a live execution context is the step this attribute describes. Message, table, stage
        /// and mode are compared, and a declared image must be present on the context. Filtering
        /// attributes are not checked: the execution context does not carry them, and reading them from
        /// the step record (OwningExtension) would cost a Retrieve on every execution.
        /// </summary>
        public bool Matches(Microsoft.Xrm.Sdk.IPluginExecutionContext context)
        {
            return string.Equals(context.MessageName, Message, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.PrimaryEntityName, Table, StringComparison.OrdinalIgnoreCase)
                && context.Stage == (int)Stage
                && context.Mode == (int)Mode
                && (string.IsNullOrEmpty(PreImage) || context.PreEntityImages.Contains(PreImageAlias))
                && (string.IsNullOrEmpty(PostImage) || context.PostEntityImages.Contains(PostImageAlias));
        }

        public override string ToString()
        {
            return string.Format("{0} on {1}, {2}, {3}", Message, Table, Stage, Mode);
        }
    }
}
