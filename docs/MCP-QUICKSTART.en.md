# MCP: stay in your AI client

[Install](INSTALLATION.en.md) · [Architecture](ARCHITECTURE.en.md) · [فارسی](MCP-QUICKSTART.fa.md)

**0.5.0 changes the execution workflow.** MCP is a native Rhino bridge. Your AI client plans, requests tools, inspects results and handles follow-ups. Rhino does not run a second AI conversation or require a local Apply click for this route. API mode remains a separate in-Rhino conversation with preview and Apply.

![Conversation and execution routes](assets/connection-architecture.png)

## Connect once

1. In Rhino, run **archtec** and open **MCP connection**.
2. Select **Codex** or **Claude Desktop** and click **Connect**. This starts loopback MCP and registers the installed bridge for the current Windows user. No API key or configuration copy is needed for this route.
3. After first setup or a plugin upgrade, refresh the client's MCP tools once. For Codex use **Settings → Plugins → MCPs**; for Claude Desktop fully quit and reopen it. Allow the modeling tools according to that client's normal approval controls.
4. In that client, ask it to use **ai-architec-rhino** and read **get_context** and **get_capabilities**.
5. Rhino shows **Connected · client name** after authenticated contact. Keep the conversation in that client. The Rhino workspace is an execution log, connection monitor and viewport.

A listening server is not a verified AI connection. A displayed client name is protocol metadata, not proof of account login. The plugin does not sign into accounts or supply a model subscription.

## First modeling request

Send this in the external AI client:

> Use only ai-architec-rhino. Read get_context and get_capabilities. Create a 1 × 2 × 3 meter closed native box with execute_plan and a new UUID request_id. Inspect its receipt and use get_objects to verify validity, closure and dimensions. Frame it with set_view and inspect get_viewport_image. Keep the entire workflow here; do not ask me to click Apply in Rhino.

The client uses its normal tool authorization. **execute_plan** validates and runs a bounded native batch. It returns actual object IDs and an Undo serial. **get_objects** measures the document's result; a successful tool call alone is not evidence of modeling quality. For complex work, build and inspect several small batches, then refine the same IDs.

**propose_plan** remains an optional visual preview. If used, the client calls **apply_plan** or **cancel_plan** itself. This is not a mandatory extra step, and the user does not have to switch to a Rhino chat.

## Stop, reconnect and uncertain outcomes

| Situation | Behavior and next action |
|---|---|
| Stop in the AI client during a tool call | A standard MCP cancellation notification cancels queued work and is checked between native operations. An in-progress Rhino kernel operation may finish before it can observe cancellation. Inspect the receipt. |
| Stop while the AI is thinking | Rhino has no autonomous planner to stop. No modeling occurs without another tool call. The listener may remain connected and idle. |
| A batch already completed | Stop does not delete completed geometry. Use native Rhino Undo if you want to reverse it. |
| Rhino MCP off/on | Keep the client open. Click **Enable connection** again; the bridge discovers the new session credential automatically. No repeated configuration copy is needed. |
| Timeout or interrupted reply | Call get_plan_status with the **same request_id** before doing anything else. Do not invent a fresh ID and duplicate the model. |
| Repeat the same execute_plan ID and identical plan | The running panel returns its recorded receipt without executing again. Reusing that ID for another plan/document is rejected. |
| Rhino or the panel was restarted | Receipts are session memory, not a persistent job database. An unknown receipt is not proof that geometry was never created; inspect the document first. |

Receipts are retained across listener off/on in the same running panel, up to 2,000 client execution requests. They are not retained across Rhino/panel restart. Each batch allows at most 100 actions and a 2 MB request. Native operations use document units/tolerance and reject invalid, stale or read-only targets.

## Modeling tools

The catalog exposes **24 native actions**: lofts, closed manifold meshes, arbitrary-vector polygon extrusion, Brep booleans and joins, one-rail sweeps, revolution, pipes, Brep offset, validated native geometry payloads, boxes, concept buildings, construction curves, existing-curve extrusion, move/scale/rotate/copy, visibility, layer/material color, selection, explicit object deletion and property reads. These are bounded operations, not unrestricted RhinoCommon or arbitrary script execution. Curves remain useful construction tools; they must not be represented as a finished solid tower.

Use **get_capabilities** for exact parameters. Use **get_objects** for validity, closure, bounds, area, volume and optional bounded point-containment probes; **set_view** and **get_viewport_image** provide actual visual verification. Image capture and broader object inspection occur only when explicitly requested by a client tool call. [Data boundaries](../rhino-plugin/PRIVACY.md)

## Other clients and browser ChatGPT

For another local STDIO MCP client, expand **Manual setup and diagnostics** and add the installed **AIArchiTec.McpBridge.exe** plus **ARCHITEC_MCP_PORT** to its MCP configuration. Keep unrelated servers. The current Windows user's session is discovered using DPAPI; the export contains no bearer token.

**Copy configuration** belongs in client settings. **Copy connection-test prompt** is a chat message after the server is loaded. Pasting configuration into chat does not register tools.

Opening browser ChatGPT does not connect Windows localhost. That route needs a supported remote connector/tunnel and account setup; this plugin does not provision a public endpoint, OAuth or browser automation. A compatible local client or the plugin's API route is the included workflow.

[Official Codex MCP guidance](https://learn.chatgpt.com/docs/extend/mcp?surface=cli) · [Local MCP clients](https://modelcontextprotocol.io/docs/develop/connect-local-servers) · [MCP cancellation semantics](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/cancellation)
