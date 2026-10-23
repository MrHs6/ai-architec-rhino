# Data and credentials

AI Archi-Tec 0.5.0 has two separate conversation routes. No API key, shared model credit or analytics endpoint is bundled.

## API route

Studio sends the user's submitted prompt, scoped recent conversation, document units/layer summary and selected/recent object metadata directly to the configured provider. Explicitly attached supported reference images are included only on submission. It does not automatically upload the entire 3DM or a viewport screenshot. Provider processing and billing follow the user's chosen account.

Optional key persistence uses Windows DPAPI CurrentUser in `%LOCALAPPDATA%\AIArchiTec\Rhino8\profiles.json` and the active-profile compatibility file `connection.json`. Each provider has independent preferences. Turning Remember off and saving removes that profile's saved key from both files; session-only keys stay in process memory. General preferences are in `preferences.json`.

## MCP route

The plugin is an execution bridge, not an AI service. The external client owns conversation, account access, model choice and tool approval. MCP requests do not call the plugin's API provider or enter its API conversation history. The local MCP workspace shows connection state and an execution log, with no second chat composer.

The server binds to `127.0.0.1`, rejects browser Origin headers, and requires a random bearer credential that rotates when restarted. Setup exports only the installed bridge path and port. Discovery uses `%LOCALAPPDATA%\AIArchiTec\Rhino8\mcp-session-<port>.json`, encrypted for the current Windows user. Normal shutdown removes the owned descriptor; stale owners fail discovery. No public listener, tunnel, OAuth flow or browser automation is provisioned.

Basic context contains units/layers and selected/recent object IDs, types and bounds. An explicit **get_objects** call may inspect other objects by ID or layer prefix, including hidden objects when requested, and returns validity, closure, area and volume. An explicit **get_viewport_image** call returns a PNG of the active viewport to the requesting AI client. The user's client/provider data policies apply to what that client receives.

**execute_plan** and **apply_plan** are write tools authorized using the external client's normal controls. They validate bounded native actions, modify the active document and record Undo. There is no additional mandatory Apply click in Rhino for MCP. No tool executes arbitrary code or shell commands. Native geometry payloads are decoded only as supported geometry types.

Operation receipts and conversation/execution logs are session memory. Receipts survive listener reconnects in the same panel, up to 2,000 requests, but not Rhino/panel restart. A stopped/uncertain request must be inspected before retrying. Completed batches remain in the document and can be undone in Rhino.

## Setup and status

Codex registration uses its installed CLI to add/replace only `ai-architec-rhino`. Claude setup merges that entry into the current user's configuration, preserves unrelated settings and creates a private sibling `.architec-backup`; that backup can contain existing settings and should remain private. No account login is automated. A plugin upgrade requires one client refresh for the new executable path.

Client names, last contact and stages come from authenticated protocol traffic; an announced name is not verification of an account identity. Local heartbeats monitor connection only and do not start AI/modeling work. Closing the client normally closes its tracked session; an idle listener may remain available until disconnected.
