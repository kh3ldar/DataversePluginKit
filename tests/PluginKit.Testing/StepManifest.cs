using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KH.PluginKit.Testing
{
    /// <summary>
    /// Renders the declared steps of an assembly as a markdown table. The table is committed as STEPS.md;
    /// a test compares the committed copy with a fresh render, so a changed registration cannot go unnoticed.
    /// </summary>
    public static class StepManifest
    {
        public static Type[] PluginTypes(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(PluginBase).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToArray();
        }

        public static string Render(Assembly assembly)
        {
            var text = new StringBuilder();
            text.Append("# Plugin steps\n\n");
            text.Append("Generated from the `[PluginStep]` declarations. Do not edit by hand: run\n");
            text.Append("`UPDATE_STEPS=1 dotnet test` (PowerShell: `$env:UPDATE_STEPS=1; dotnet test; Remove-Item Env:UPDATE_STEPS`) to regenerate.\n\n");
            text.Append("| Plugin | Message | Table | Stage | Mode | Filtering attributes | Pre-image | Post-image |\n");
            text.Append("| --- | --- | --- | --- | --- | --- | --- | --- |\n");

            foreach (var type in PluginTypes(assembly))
            {
                var steps = PluginBase.StepsOf(type)
                    .OrderBy(s => s.Message, StringComparer.Ordinal).ThenBy(s => s.Table, StringComparer.Ordinal);
                foreach (var s in steps)
                {
                    text.AppendFormat("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |\n",
                        type.Name, s.Message, s.Table, s.Stage, s.Mode,
                        Cell(s.FilteringAttributes), Cell(s.PreImage), Cell(s.PostImage));
                }
            }
            return text.ToString();
        }

        /// <summary>Walks up from the test output folder to the folder that holds the solution file.</summary>
        public static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DataversePluginKit.slnx")))
                dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("DataversePluginKit.slnx not found above " + AppContext.BaseDirectory);
            return dir.FullName;
        }

        private static string Cell(string value) => string.IsNullOrEmpty(value) ? "-" : "`" + value + "`";
    }
}
