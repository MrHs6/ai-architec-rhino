# Architecture and execution model

[Overview](../README.md) · [Installation](INSTALLATION.en.md) · [MCP setup](MCP-QUICKSTART.en.md) · [Provider protocols](PROVIDERS.md)

![Connection architecture](assets/connection-architecture.png)

## Conversation routes

| Route | Conversation host | Connection | Result path |
|---|---|---|---|
| API | Rhino Studio | Personal provider endpoint, key and selected model | Structured response → validation → viewport preview → explicit Apply |
| MCP | Codex, Claude Desktop or compatible local client | STDIO bridge → authenticated loopback HTTP | Client tool call → validation → viewport preview → explicit Apply |
| Offline example | Rhino Studio | Limited deterministic local planner | Supported numeric example → the same preview/Apply path |

API profiles and MCP are independent. Starting MCP does not bind the Rhino composer to an online account. In MCP mode the composer is hidden; the AI client supplies requests and references. Analyze's local geometry audit is available without an AI connection.

## Repository modules

| Module | Responsibility | Relevant sources |
|---|---|---|
| `ArchiTec.Rhino` | Plugin registration, commands, WPF/Eto host, document binding, preview and native geometry execution | [Panel](../rhino-plugin/src/ArchiTec.Rhino/ArchiTecPanel.cs), [Studio](../rhino-plugin/src/ArchiTec.Rhino/Views/StudioView.xaml), [geometry engine](../rhino-plugin/src/ArchiTec.Rhino/GeometryEngine.cs) |
| `ArchiTec.Core` | Action contracts, workspace rules, provider adapters, MCP HTTP protocol, connection tracking and encrypted session discovery | [Contracts](../rhino-plugin/src/ArchiTec.Core/Contracts.cs), [MCP server](../rhino-plugin/src/ArchiTec.Core/McpServer.cs), [provider profiles](../rhino-plugin/src/ArchiTec.Core/ProviderProfiles.cs) |
| `ArchiTec.McpBridge` | Stable local STDIO surface, UTF-8 JSON-RPC, current-user credential discovery and reconnection | [Reconnecting bridge](../rhino-plugin/src/ArchiTec.McpBridge/ReconnectingBridge.cs) |
| `ArchiTec.Tests` | Core/provider/protocol/workspace checks and a real bridge-process reconnect fixture | [Tests](../rhino-plugin/tests/ArchiTec.Tests), [reconnect fixture](../rhino-plugin/tests/ArchiTec.Tests/BridgeReconnectTests.cs) |
| Packaging | Per-user Yak payload, Windows installers, registration repair and release checksums | [Scripts](../rhino-plugin/scripts), [distribution](../rhino-plugin/docs/DISTRIBUTION.md) |

## MCP tool contract

| Tool | Purpose | Geometry effect |
|---|---|---|
| `get_context` | Active document ID, units, layers and limited selected/recent object metadata | Read-only |
| `post_message` | Show a conversational answer or analysis in the selected workspace | No geometry change |
| `propose_plan` | Validate supported actions for the current document and display a preview | Preview only |
| `get_plan_status` | Read awaiting/applied/cancelled/failed status and confirmed output IDs | Read-only |

The client reads the current `document_id` first. Supported plans contain declared native operations such as building/box/curve creation, extrusion, scoped transformations and layer operations. No tool executes arbitrary model-generated code. The user clicks Apply in Rhino. Native operations run on the Rhino UI thread, report actual object IDs/bounds and participate in batch Undo. Stale document, unit or target changes require a fresh plan.

## Reconnection lifecycle

The client initializes a persistent STDIO bridge. Tool discovery stays available even before Rhino's listener starts. Each request and background heartbeat checks the current user's encrypted session descriptor. When Rhino MCP restarts, the descriptor carries the new token and the bridge performs a new authenticated handshake using the client's announced metadata.

Pre-dispatch HTTP 401/404 rejections can reinitialize and retry once. Timeouts, uncertain network failures, HTTP 500 and application tool errors are **not replayed**. When a request's outcome is uncertain, inspect the existing preview or `get_plan_status` before sending another model request. Normal bridge shutdown closes its authenticated session. Client status is based on observed contact, not proof of account authentication.

## Data boundaries

- Local MCP binds to loopback, requires a rotating bearer credential and rejects browser Origin headers. This release does not create a public listener or cloud tunnel.
- Setup exports the installed bridge path and port, without a token. Discovery uses Windows CurrentUser DPAPI. Remembered API keys are also per-user encrypted; session-only keys are not persisted.
- AI requests use prompts, recent conversation and limited unit/layer/selected-or-recent object context. The entire 3DM and viewport image are not automatically uploaded. Explicitly attached supported reference images are sent with the submitted AI request.
- Codex registration uses its installed CLI; Claude configuration merges preserve other entries and create a private sibling backup. The plugin does not log into external accounts.

Read [PRIVACY.md](../rhino-plugin/PRIVACY.md) for storage details and [VALIDATION.md](../rhino-plugin/docs/VALIDATION.md) for measured acceptance and remaining limits.
