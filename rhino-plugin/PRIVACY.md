# Data and credentials

The API-key workflow is bring-your-own-key, as requested by the project owner. Requests go directly from the plugin to the explicitly configured API source. The earlier server-only credential architecture in the handover was superseded by this requirement.

No API credential is bundled. Optional persistence uses Windows DPAPI CurrentUser in `%LOCALAPPDATA%\AIArchiTec\Rhino8\profiles.json` and the active-profile compatibility file `connection.json`. Each provider has independent preferences. Turning Remember off and saving removes that profile's saved key from both copies. Without Remember enabled, the key stays in process memory. General preferences are stored separately in `preferences.json`. There is no plugin analytics or telemetry endpoint.

An AI request includes the user's prompt, recent conversation, document units/layer summary, and selected or recently panel-created object IDs/types/layers/bounding boxes. It does not upload the entire Rhino file or viewport screenshot. An explicitly attached reference image is sent only when the user submits an AI request to a supported vision model. Provider handling and billing follow the user's chosen service.

The local MCP server binds only to 127.0.0.1, rejects browser Origin headers, requires a random bearer token, and starts only when requested. The token rotates on restart. Its tools can read limited document context or propose validated actions. Only the Rhino panel's Apply executes a plan. No MCP tool runs arbitrary scripts or bypasses approval.

The current MCP setup export contains the bridge path and port, without a token. The bridge discovers the active session through a CurrentUser DPAPI-encrypted descriptor in %LOCALAPPDATA%/AIArchiTec/Rhino8/mcp-session-<port>.json. It is removed when that server stops normally; stale descriptors fail when the owner process has exited. Reload the client connection after a server restart. Legacy exports containing a token should be replaced with the new setup. No public listener or tunnel is created by this plugin.

The panel reports client-announced names, last activity and workflow stages from actual authenticated MCP traffic. These names do not verify account identity. The local bridge sends a heartbeat every 15 seconds and closes its session on normal shutdown. post_message adds a client response to the workspace conversation without changing geometry. Copy for MCP copies user text locally; sending to a client chat remains an explicit user action.
