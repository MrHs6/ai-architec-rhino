# Installed Rhino API and Grasshopper acceptance

Run these only in a **disposable Rhino document**. The Grasshopper tests deliberately create graphs, bake boxes, perform Undo/Redo and exercise failed-bake rollback. The core, MCP and mock-planner checks do not use saved user models or paid API credentials. The explicitly scheduled **live provider acceptance** below is different: it uses the remembered personal OpenRouter profile and can incur API charges. Never run it implicitly as part of a build or the automatic core suite.

## Core / protocol suite

From the repository root on Windows with .NET SDK 8:

```powershell
dotnet build rhino-plugin/AIArchiTec.sln -c Release -p:BuildWithNuGet=true
dotnet rhino-plugin/tests/ArchiTec.Tests/bin/Release/net8.0/ArchiTec.Tests.dll
```

Allow loopback HTTP sockets for integration tests. Native runtime tests also require installed licensed Rhino 8.31+ and its Grasshopper SDK.

## Real MCP acceptance

1. Build/install the candidate, open an empty Rhino document and run `archtec`.
2. Enable the test MCP connection on an unused local port (for example 5190).
3. Run the Python 3 test client below. It talks to the actual STDIO bridge and encrypted local discovery; it does not read or print a token. Each run intentionally creates a new test graph and one baked box.

```powershell
python rhino-plugin/tests/grasshopper_mcp_acceptance.py --port 5190
```

The assertions cover component discovery, a seven-node graph, an identical UUID receipt retry, in-place update and a valid closed 5 × 4 × 3 document-unit solid of volume 60. Use a Meters template to reproduce the recorded meter dimensions. JSON requests are recorded before execution; receipts/measurements are saved immediately to the selected output folder. After an uncertain write result, inspect the saved request/receipt before rerunning.

## In-Rhino API path without charges

Build this helper against the installed SDK:

```powershell
dotnet build rhino-plugin/tests/ArchiTec.RuntimeTests/ArchiTec.RuntimeTests.csproj -c Release
```

After the MCP test in the same disposable session, use Rhino's C# ScriptEditor to load the helper and schedule its test. Substitute absolute local paths:

```csharp
using System.Reflection;
var helper = Assembly.LoadFrom(@"C:\YOUR\REPO\rhino-plugin\tests\ArchiTec.RuntimeTests\bin\Release\net8.0-windows\ArchiTec.RuntimeTests.dll");
helper.GetType("ArchiTec.RuntimeTests.RuntimeAcceptance")
    .GetMethod("Schedule")
    .Invoke(null, new object[] {
        @"C:\YOUR\REPO\outputs\grasshopper-acceptance",
        @"C:\YOUR\REPO\outputs\grasshopper-acceptance\example-definition-plan.json"
    });
```

The helper waits for Rhino Idle, swaps the planner in memory, selects the actual API target and calls the panel's Send/Apply path. It restores the original planner afterward. It tests graph isolation/update, native geometry, separate Undo stacks, rollback, `.gh` export and reopen. The report explicitly marks `mock_api: true`; this is not live provider generation. Close the disposable Rhino process before rebuilding a loaded helper, or build it with a unique `RuntimeTestAssemblyName` to avoid an assembly lock.

Saving/reopening a graph does not persist bridge replacement ownership. Ordinary saved graphs remain inspectable; this candidate intentionally rejects their automatic replacement.

## Live personal API acceptance — explicit scheduling only

The `LiveProviderAcceptance` helper uses the installed panel's real `ConnectAsync`, `SendAsync`, preview and `ApplyPending` path. It does not replace the provider, change proxy/VPN settings, bypass TLS, or substitute another model.

Before scheduling:

1. Save any user work and open an **empty, visible, disposable** Rhino document with real length units; hidden, locked and reference geometry also disqualify it.
2. Open `archtec`. Configure the user's own OpenRouter key in **API profiles**, with **Remember securely after restarting Rhino** enabled. The helper reads that remembered profile in-process through `SettingsStore`; do not put a key in a script, command, output folder or report.
3. Explicitly choose the exact compatible catalog model ID to test, considering its current pricing. The helper accepts this ID as an argument and refuses to silently substitute another one.
4. Expect **three real inference requests** if the run completes: a short Persian greeting, a native box plan and a follow-up movement plan. Authentication/model discovery are separate API requests. Even a failed inference may be billable under the provider's terms; the helper does not retry or purchase credit automatically.

After building/loading the helper as above, schedule from Rhino's C# ScriptEditor using absolute paths and the explicitly chosen model ID:

```csharp
helper.GetType("ArchiTec.RuntimeTests.LiveProviderAcceptance")
    .GetMethod("Schedule")
    .Invoke(null, new object[] {
        @"C:\YOUR\REPO\outputs\live-api-acceptance",
        "EXACT_PROVIDER_MODEL_ID"
    });
```

It waits for Rhino Idle and verifies the following sequence:

- A saved, disconnected profile rejects a greeting instead of silently using local examples. This preconnection check sends no cloud request.
- The real API authenticates and discovers compatible models; the supplied exact model is selected and retained in the working profile.
- A Persian greeting returns text with no plan or geometry.
- A 2 × 4 × 3 meter native box is proposed without document mutation, then explicitly applied and measured as a valid closed Brep of volume 24 m³.
- A follow-up moves that same object 3 meters along positive world X, preserving its GUID and dimensions; proposal and Apply are checked separately.
- `Live-API-Box-Moved.3dm` is saved and reopened to verify the native result.

`live-provider-acceptance.json` is replaced atomically after each completed step and on failure. It contains the plug-in version, stage, timings, measured results and safe diagnostics. It excludes keys, account identifiers, raw provider messages and response bodies. `requested_model_id` records the explicit input; `actual_model_id` remains null until the exact catalog model is selected. That field alone does not prove successful generation: check the completed stages and final `status`. The selected working model remains available after success. An actual connection failure retains its failed profile and actionable panel error.

Do not rerun after an uncertain write merely because a report is delayed. Inspect the recorded stage and current Rhino objects first. The live run intentionally changes the disposable document and does not promise provider availability, model quality or engineering validity.

## Existing failed-connection acceptance — no new cloud requests

`ApiFailureAcceptance` requires a **real, just-failed Connect**, with no active provider and an actionable connection error still in the panel. Use an empty disposable interactive document. It does not force an API failure, change the network, or infer that HTTP 403 means an invalid key. If those prerequisites are absent, its report says `unavailable`.

```csharp
helper.GetType("ArchiTec.RuntimeTests.ApiFailureAcceptance")
    .GetMethod("Schedule")
    .Invoke(null, new object[] {
        @"C:\YOUR\REPO\outputs\api-failure-acceptance"
    });
```

The fixture checks missing-model selection and stale connection UI using a detached view; rejects a fixture key for the wrong service locally without replacing the saved profile; proves actual Send after the existing failed Connect creates no offline plan; then invokes the real **Disconnect API / use offline tools** handler and verifies that the same box request can produce a local preview. It never clicks Apply or commits geometry. Finally it clears the preview and restores the real failed-connection status. Its sanitized `api-failure-acceptance.json` records the stages and zero cloud requests from this fixture.
