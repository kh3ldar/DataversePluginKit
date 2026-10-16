using System;
using System.Collections.Generic;
using KH.PluginKit;
using KH.PluginKit.Testing;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace KH.PluginKit.Tests
{
    public class LifecycleTests
    {
        [PluginStep("Update", "contact", PluginStage.PreOperation, PreImage = "firstname")]
        private sealed class Recorder : PluginBase
        {
            public List<string> Calls { get; } = new List<string>();
            public Exception FailIn { get; set; }
            public string FailAt { get; set; }

            private void Step(string name)
            {
                Calls.Add(name);
                if (FailAt == name) throw FailIn;
            }

            protected override void Authorize(PluginRun run) => Step("authorize");
            protected override void Validate(PluginRun run) => Step("validate");
            protected override void Execute(PluginRun run) => Step("execute");
            protected override void Compensate(PluginRun run, Exception error) => Step("compensate");
        }

        private sealed class Undeclared : PluginBase
        {
            protected override void Execute(PluginRun run) { }
        }

        private static FakeServiceProvider Provider(string message = "Update", string table = "contact",
            int stage = 20, bool withPreImage = true, int mode = 0)
        {
            var context = FakePluginContext.For(message, table, stage, new Entity(table, Guid.NewGuid()), mode);
            if (withPreImage) context.PreEntityImages["PreImage"] = new Entity(table);
            return new FakeServiceProvider(context);
        }

        [Fact]
        public void Stages_run_in_order()
        {
            var plugin = new Recorder();
            plugin.Execute(Provider());
            Assert.Equal(new[] { "authorize", "validate", "execute" }, plugin.Calls);
        }

        [Fact]
        public void Execute_is_sealed_so_the_order_cannot_be_replaced()
        {
            var method = typeof(PluginBase).GetMethod("Execute", new[] { typeof(IServiceProvider) });
            // An interface implementation is virtual but final; what matters is that it cannot be overridden.
            Assert.True(!method.IsVirtual || method.IsFinal);
        }

        [Fact]
        public void Refusal_in_authorize_skips_validate_execute_and_compensate()
        {
            var plugin = new Recorder { FailAt = "authorize", FailIn = new InvalidPluginExecutionException("no") };
            Assert.Throws<InvalidPluginExecutionException>(() => plugin.Execute(Provider()));
            Assert.Equal(new[] { "authorize" }, plugin.Calls);
        }

        [Fact]
        public void Failure_in_execute_compensates_and_still_propagates()
        {
            var boom = new InvalidOperationException("boom");
            var plugin = new Recorder { FailAt = "execute", FailIn = boom };

            var thrown = Assert.Throws<InvalidOperationException>(() => plugin.Execute(Provider()));

            Assert.Same(boom, thrown);
            Assert.Equal(new[] { "authorize", "validate", "execute", "compensate" }, plugin.Calls);
        }

        [Fact]
        public void A_failing_compensation_does_not_replace_the_original_error()
        {
            var provider = Provider();
            var plugin = new CompensationThrows();

            var thrown = Assert.Throws<InvalidOperationException>(() => plugin.Execute(provider));

            Assert.Equal("first", thrown.Message);
            Assert.Contains(provider.Tracing.Lines, l => l.Contains("Compensation failed too: second"));
        }

        [PluginStep("Update", "contact", PluginStage.PreOperation)]
        private sealed class CompensationThrows : PluginBase
        {
            protected override void Execute(PluginRun run) => throw new InvalidOperationException("first");
            protected override void Compensate(PluginRun run, Exception error) => throw new InvalidOperationException("second");
        }

        [Theory]
        [InlineData("Create", "contact", 20, 0)]
        [InlineData("Update", "account", 20, 0)]
        [InlineData("Update", "contact", 40, 0)]
        [InlineData("Update", "contact", 20, 1)]
        public void A_mis_wired_step_is_refused_before_any_stage_runs(string message, string table, int stage, int mode)
        {
            var plugin = new Recorder();
            var ex = Assert.Throws<InvalidPluginExecutionException>(
                () => plugin.Execute(Provider(message, table, stage, mode: mode)));

            Assert.Contains("not registered the way it declares", ex.Message);
            Assert.Empty(plugin.Calls);
        }

        [Fact]
        public void A_declared_pre_image_that_the_step_lacks_is_refused()
        {
            var plugin = new Recorder();
            Assert.Throws<InvalidPluginExecutionException>(() => plugin.Execute(Provider(withPreImage: false)));
            Assert.Empty(plugin.Calls);
        }

        [Fact]
        public void A_plugin_without_a_declared_step_fails_loudly()
        {
            Assert.Throws<InvalidOperationException>(() => new Undeclared().Execute(Provider()));
        }

        [Fact]
        public void Trace_lines_carry_the_correlation_id()
        {
            var provider = Provider();
            new Recorder().Execute(provider);

            var id = provider.Context.CorrelationId.ToString();
            Assert.NotEmpty(provider.Tracing.Lines);
            Assert.All(provider.Tracing.Lines, line => Assert.StartsWith("[" + id + "]", line));
        }

        [Fact]
        public void The_service_is_created_for_the_context_user()
        {
            var provider = Provider();
            new Recorder().Execute(provider);
            Assert.Equal(provider.Context.UserId, provider.Service.CreatedFor);
        }

        [Fact]
        public void State_carries_a_value_from_execute_to_compensate_without_a_field()
        {
            var plugin = new StateCarrier();
            var provider = Provider();
            Assert.Throws<InvalidOperationException>(() => plugin.Execute(provider));
            Assert.Contains(provider.Tracing.Lines, l => l.Contains("saw 42"));
        }

        [PluginStep("Update", "contact", PluginStage.PreOperation)]
        private sealed class StateCarrier : PluginBase
        {
            protected override void Execute(PluginRun run)
            {
                run.State["n"] = 42;
                throw new InvalidOperationException();
            }

            protected override void Compensate(PluginRun run, Exception error)
                => run.Trace.Info("saw {0}", run.State["n"]);
        }
    }
}
