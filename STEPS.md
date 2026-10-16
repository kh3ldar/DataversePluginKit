# Plugin steps

Generated from the `[PluginStep]` declarations. Do not edit by hand: run
`UPDATE_STEPS=1 dotnet test` (PowerShell: `$env:UPDATE_STEPS=1; dotnet test; Remove-Item Env:UPDATE_STEPS`) to regenerate.

| Plugin | Message | Table | Stage | Mode | Filtering attributes | Pre-image | Post-image |
| --- | --- | --- | --- | --- | --- | --- | --- |
| AccountFollowUpPlugin | Create | account | PostOperation | Synchronous | - | - | - |
| OpportunityDiscountPlugin | Create | opportunity | PreOperation | Synchronous | - | - | - |
| OpportunityDiscountPlugin | Update | opportunity | PreOperation | Synchronous | `discountpercentage` | `discountpercentage` | - |
