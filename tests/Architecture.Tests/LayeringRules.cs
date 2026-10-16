using System.Linq;
using System.Reflection;
using KH.PluginKit;
using KH.Sample.Adapters;
using KH.Sample.Domain;
using KH.Sample.Plugins;
using Microsoft.Xrm.Sdk;
using NetArchTest.Rules;
using Xunit;

namespace KH.Architecture.Tests
{
    /// <summary>
    /// The layering as executable rules: domain at the bottom with no platform references, adapters
    /// above it doing the SDK calls, plugin hosts on top depending on both.
    /// </summary>
    public class LayeringRules
    {
        private static readonly Assembly Domain = typeof(DiscountPolicy).Assembly;
        private static readonly Assembly Adapters = typeof(FollowUpStore).Assembly;
        private static readonly Assembly Plugins = typeof(OpportunityDiscountPlugin).Assembly;

        private static void AssertPasses(TestResult result)
        {
            var offenders = result.FailingTypeNames ?? new string[0];
            Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Domain_has_no_sdk_http_or_kit_types()
        {
            AssertPasses(Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(
                "Microsoft.Xrm", "Microsoft.PowerPlatform", "System.Net.Http", "KH.PluginKit").GetResult());
        }

        [Fact]
        public void Domain_references_no_sdk_assembly_at_all()
        {
            // The type-level rule above sees usages; this one sees the assembly manifest.
            var names = Domain.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.DoesNotContain(names, n => n.StartsWith("Microsoft.Xrm") || n.StartsWith("Microsoft.PowerPlatform")
                || n == "System.Net.Http" || n == "KH.PluginKit");
        }

        [Fact]
        public void Adapters_do_not_reach_up_into_plugins_or_the_kit()
        {
            AssertPasses(Types.InAssembly(Adapters).ShouldNot().HaveDependencyOnAny(
                "KH.Sample.Plugins", "KH.PluginKit").GetResult());
        }

        [Fact]
        public void Adapters_make_no_http_calls()
        {
            AssertPasses(Types.InAssembly(Adapters).ShouldNot().HaveDependencyOn("System.Net.Http").GetResult());
        }

        [Fact]
        public void Every_plugin_derives_from_the_base_class()
        {
            AssertPasses(Types.InAssembly(Plugins).That().ImplementInterface(typeof(IPlugin)).And().AreNotAbstract()
                .Should().Inherit(typeof(PluginBase)).GetResult());
        }

        [Fact]
        public void Plugins_never_implement_the_sdk_interface_directly()
        {
            // Inherit() above is satisfied by PluginBase itself, so check the declared interface list too.
            var direct = Plugins.GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IPlugin))
                    && (t.BaseType == null || !t.BaseType.GetInterfaces().Contains(typeof(IPlugin))))
                .Select(t => t.Name).ToArray();
            Assert.Empty(direct);
        }

        [Fact]
        public void Plugin_hosts_are_sealed_so_the_lifecycle_has_one_shape()
        {
            AssertPasses(Types.InAssembly(Plugins).That().Inherit(typeof(PluginBase)).Should().BeSealed().GetResult());
        }

        [Fact]
        public void Nothing_below_the_hosts_depends_on_them()
        {
            AssertPasses(Types.InAssemblies(new[] { Domain, Adapters })
                .ShouldNot().HaveDependencyOn("KH.Sample.Plugins").GetResult());
        }

        [Fact]
        public void The_base_class_rule_flags_a_plugin_that_skips_it()
        {
            // A rule that cannot fail proves nothing. This fixture type breaks the "derive from the base
            // class" rule on purpose, and the same query must flag it.
            var result = Types.InAssembly(typeof(LayeringRules).Assembly)
                .That().ResideInNamespace("KH.Architecture.Tests.Fixtures").And().ImplementInterface(typeof(IPlugin))
                .Should().Inherit(typeof(PluginBase)).GetResult();

            Assert.False(result.IsSuccessful);
            Assert.Contains(result.FailingTypeNames, n => n.EndsWith("RawPlugin"));
        }
    }
}

namespace KH.Architecture.Tests.Fixtures
{
    public sealed class RawPlugin : Microsoft.Xrm.Sdk.IPlugin
    {
        public void Execute(System.IServiceProvider serviceProvider) { }
    }
}
