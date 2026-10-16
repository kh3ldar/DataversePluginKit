using System;
using System.IO;
using System.Linq;
using KH.PluginKit.Testing;
using KH.Sample.Plugins;
using Xunit;

namespace KH.Sample.Tests
{
    /// <summary>The two gates that keep declared registrations honest.</summary>
    public class ManifestGateTests
    {
        private static readonly System.Reflection.Assembly Plugins = typeof(OpportunityDiscountPlugin).Assembly;

        [Fact]
        public void StepsMd_matches_the_declared_registrations()
        {
            var path = Path.Combine(StepManifest.RepoRoot(), "STEPS.md");
            var fresh = StepManifest.Render(Plugins);

            if (Environment.GetEnvironmentVariable("UPDATE_STEPS") == "1")
            {
                File.WriteAllText(path, fresh);
                return;
            }

            Assert.True(File.Exists(path), "STEPS.md is missing. Run with UPDATE_STEPS=1 to create it.");
            var committed = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.True(committed == fresh,
                "STEPS.md is out of date with the [PluginStep] declarations. Run the tests with UPDATE_STEPS=1 and commit the result.");
        }

        [Fact]
        public void Every_plugin_has_a_test_class_named_after_it()
        {
            var testTypes = typeof(ManifestGateTests).Assembly.GetTypes().Select(t => t.Name).ToArray();
            var missing = StepManifest.PluginTypes(Plugins)
                .Select(t => t.Name)
                .Where(name => !testTypes.Contains(name + "Tests"))
                .ToArray();

            Assert.True(missing.Length == 0, "No <Plugin>Tests class for: " + string.Join(", ", missing));
        }

        [Fact]
        public void The_gates_find_the_sample_plugins()
        {
            // Guards against both gates passing because the reflection found nothing.
            Assert.Equal(2, StepManifest.PluginTypes(Plugins).Length);
        }
    }
}
