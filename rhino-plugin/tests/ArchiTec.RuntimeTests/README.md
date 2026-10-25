# Installed Grasshopper acceptance

Run these only in a **disposable Rhino document**. The tests deliberately create graphs, bake boxes, perform Undo/Redo and exercise failed-bake rollback. They do not use saved user models or paid API credentials.

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
