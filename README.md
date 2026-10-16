# DataversePluginKit

A small kit for writing Dataverse plugins with a fixed lifecycle, registrations declared in code, and tests that keep both honest. It comes with a sample on out-of-the-box tables (opportunity, account, task), split into domain, adapters and plugin hosts.

## Why it exists

Most plugin code is one `IPlugin.Execute` that unpacks the context, checks a few things, calls the service, and grows from there. Authorisation, validation and clean-up end up interleaved with the work, in a different order in every plugin. The step registration lives only in the environment, so a wrong message, stage or missing image shows up at runtime, if at all.

The patterns here were built for a production Dynamics 365 implementation. This repo is a clean, generic rewrite of the ideas, not an extract of that code.

## What it does

- `PluginBase` implements `IPlugin` with a sealed lifecycle: bind, check registration, authorise, validate, execute, compensate. A plugin overrides the hooks it needs.
- `[PluginStep]` declares each step on the plugin class: message, table, stage, mode, filtering attributes, pre-image and post-image. The base class compares the live context with the declarations before any hook runs.
- `PluginRun` carries the context, an organization service, a `PluginTrace` that starts every line with the correlation id, and per-execution state.
- `STEPS.md` is generated from the declarations. A test fails when it is stale.
- A second test fails when a plugin has no `<Plugin>Tests` class.
- Layering rules run as tests with [NetArchTest](https://github.com/BenMorris/NetArchTest).

## Design highlights

**Sealed lifecycle.** `Execute(IServiceProvider)` is not virtual. Authorise runs before validate, so a refused caller never reaches the validation rules. Compensate runs only when execute throws, then the original exception propagates. If compensation itself throws, that is traced and the original exception still wins.

**Registrations as code.** The check covers message, table, stage and mode, and that a declared image is present. It does not check filtering attributes: the execution context does not carry them, and reading them from the step record (`OwningExtension`) would cost a Retrieve on every execution. They are declared so they appear in `STEPS.md`, and nothing more. A plugin with no `[PluginStep]` fails with an `InvalidOperationException` instead of running unchecked.

**No state in fields.** The platform caches a plugin instance and reuses it, and Microsoft's [guidance](https://learn.microsoft.com/power-apps/developer/data-platform/best-practices/business-logic/develop-iplugin-implementations-stateless) is to write `IPlugin` implementations as stateless. `PluginRun.State` carries a value from execute to compensate instead.

**Which user.** The run's service is created for `context.UserId`, the user the step runs as. The sample's approver check uses `InitiatingUserId`, the user who started the operation.

**Sample, three layers.**

| Project | Contains | May reference |
| --- | --- | --- |
| `Sample.Domain` | `DiscountPolicy`, `FollowUpPlanner`, ports | nothing (netstandard2.0, no packages) |
| `Sample.Adapters` | `ApproverDirectory`, `FollowUpStore` (the SDK calls) | domain |
| `Sample.Plugins` | `OpportunityDiscountPlugin`, `AccountFollowUpPlugin` | kit, domain, adapters |

`OpportunityDiscountPlugin` uses authorise (a discount above 10% needs a member of an approver team), validate (more than 30% is refused) and execute (stamps a tier on the row). `AccountFollowUpPlugin` creates a task and stamps a date on the account, and its compensate step deletes the task if the stamp fails. Its step is synchronous, where that compensation is redundant: the platform's transaction rolls the task back, and Microsoft [documents](https://learn.microsoft.com/troubleshoot/power-platform/dataverse/plug-in-execution/dataverse-plug-ins-errors#transaction-errors) that a failed data operation in a synchronous plugin ends the transaction, so the delete would fail too. The kit traces that failure and rethrows the first one. The compensation is there to show the mechanism for effects a transaction does not cover, such as a call to another system.

## Architecture

```
src/
  PluginKit/        PluginBase, PluginRun, PluginTrace, [PluginStep]     (net462, net10.0)
  Sample.Domain/    pure rules and ports                                  (netstandard2.0)
  Sample.Adapters/  organization-service calls                            (net462, net10.0)
  Sample.Plugins/   plugin hosts                                          (net462, net10.0)
tests/
  PluginKit.Testing/    hand-rolled fakes and the STEPS.md generator
  PluginKit.Tests/      lifecycle tests
  Sample.Tests/         domain, adapter and plugin tests, plus the two manifest gates
  Architecture.Tests/   layering rules
STEPS.md                generated step table
```

The architecture rules: the domain has no SDK, HTTP or kit references (checked on types and on the assembly manifest); adapters never reference plugins or the kit; every concrete `IPlugin` derives from `PluginBase`; hosts are sealed; nothing below the hosts references them. One test runs the base-class rule against a deliberately bad fixture, to show the rule can fail.

## Hosting

The plugin assembly targets `net462`. Dataverse supports plug-in assemblies built for .NET Framework 4.6.2 through 4.8, and none built for .NET Core ([Microsoft Learn](https://learn.microsoft.com/power-apps/developer/data-platform/supported-customizations#support-for-net-framework-versions)). 4.6.2 is the lowest supported target. The same page recommends 4.8 for plug-in projects; moving means replacing `net462` in the project files and `build/XrmSdk.props`.

The tests are meant to run where there is no .NET Framework, so the kit, adapters and plugins multi-target:

- `net462` builds against `Microsoft.CrmSdk.CoreAssemblies` and is the deployable build.
- `net10.0` builds against `Microsoft.PowerPlatform.Dataverse.Client`, which ships `Microsoft.Xrm.Sdk` for modern .NET. The tests run against this build.

The source is identical for both. The net462 reference assemblies come from the `Microsoft.NETFramework.ReferenceAssemblies` NuGet package, not from an installed targeting pack. That package is meant to make the net462 build work on macOS and Linux too, but this repo has only been built on Windows.

## Configuration

None at runtime. The sample's limits (10% and 30%) are constructor defaults of `DiscountPolicy`. The sample uses the `kh_` prefix for its two custom columns (`kh_discounttier`, `kh_followupdue`) and looks for a team named `Discount Approvers`. None of these exist in any environment; they are only named in code.

## Scope and trade-offs

- **Not run against a live Dataverse org.** Everything is verified offline: a build and tests against hand-written fakes. The fakes implement the SDK interfaces and prove the logic, not the platform's behaviour. In particular the sample query, column names, image aliases and the plugin package have not been registered or executed anywhere.
- The registration check matches the context, not the environment. It cannot tell you a step is missing, only that a step that ran is not one the class declares.
- Custom API steps, stage 30, child pipelines and alternate keys are not modelled.
- `Authorize` and `Validate` are separate hooks by convention. Nothing stops a plugin from putting rules in the wrong one.
- Reflection reads the attributes on every execution. It is cheap next to a service call, but it is not free.
- The net10.0 build pulls in versions of `System.Security.Cryptography.Xml` and `System.Security.Cryptography.Pkcs` that NuGet's audit flags (`NU1902`, `NU1903`), transitively through `Microsoft.PowerPlatform.Dataverse.Client`. The build prints those warnings and does not fail on them; they are not suppressed. The net462 build that goes into the plugin package does not reference either package, and no code in this repo calls them.

## Building

Needs the .NET 10 SDK. Built and tested on Windows only; macOS and Linux are untested.

```
dotnet build
dotnet test
```

`STEPS.md` is regenerated by running the tests with `UPDATE_STEPS=1` (PowerShell: `$env:UPDATE_STEPS=1; dotnet test; Remove-Item Env:UPDATE_STEPS`, since a PowerShell variable stays set for the session). Without it the same test compares and fails when the file is stale.

Every build of `Sample.Plugins`, Debug or Release, also produces a plugin package (`kh_SamplePlugins.1.0.0.nupkg` in `src/Sample.Plugins/bin/<configuration>/`), using `Microsoft.PowerApps.MSBuild.Plugin`:

```
dotnet build src/Sample.Plugins -c Release
```

The package is not trimmed: it contains the sample assemblies plus every dependency copied to the output, including the SDK assemblies the sandbox already provides, and a `net10.0` copy of the plugin assembly from multi-targeting. A real package would trim that. It was not uploaded anywhere.

### On running it locally

There is no local Dataverse. To run a plugin you hand it a fake `IServiceProvider`; see `tests/PluginKit.Testing/Fakes.cs` and any test in `tests/Sample.Tests`. The fakes record calls and let a test inject faults and query results. FakeXrmEasy is not used, because of its licence.

## Stack

C#, .NET Framework 4.6.2 and .NET 10, xUnit 2.9.3, NetArchTest.Rules 1.3.2, Microsoft.CrmSdk.CoreAssemblies, Microsoft.PowerPlatform.Dataverse.Client, Microsoft.PowerApps.MSBuild.Plugin. Package versions are set in `build/*.props` and the project files.

MIT licensed.
