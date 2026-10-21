# Data and credentials

The API-key workflow is bring-your-own-key, as requested by the project owner. Requests go directly from the plugin to the explicitly configured API source. The earlier server-only credential architecture in the handover was superseded by this requirement.

No API credential is bundled. Optional persistence uses Windows DPAPI CurrentUser in `%LOCALAPPDATA%\AIArchiTec\Rhino8\profiles.json` and the active-profile compatibility file `connection.json`. Each provider has independent preferences. Turning Remember off and saving removes that profile's saved key from both copies. Without Remember enabled, the key stays in process memory. General preferences are stored separately in `preferences.json`. There is no plugin analytics or telemetry endpoint.

An AI request includes the user's prompt, recent conversation, document units/layer summary, and selected or recently panel-created object IDs/types/layers/bounding boxes. It does not upload the entire Rhino file or viewport screenshot. An explicitly attached reference image is sent only when the user submits an AI request to a supported vision model. Provider handling and billing follow the user's chosen service.

The local MCP server binds only to 127.0.0.1, rejects browser Origin headers, requires a random bearer token, and starts only when requested. The token rotates on restart. Its tools can read limited document context or propose validated actions. Only the Rhino panel's Apply executes a plan. No MCP tool runs arbitrary scripts or bypasses approval.

Clipboard client configuration contains the local MCP token. Paste it only into the intended desktop client configuration. No public listener or tunnel is created by this plugin.
