# Chat there. Build here.

AI Archi-Tec 0.4.2 has two independent conversation routes. The interface and built-in help are English; your prompts can be Persian.

## Use Codex or Claude Desktop

1. In Rhino, run **archtec**, open **MCP connection**, and choose **Codex** or **Claude Desktop**.
2. Click **Connect to Codex** or **Connect to Claude Desktop**. This enables the local server, registers this user's installed bridge, and opens the client when it can be found. No configuration or test-prompt copy is needed in the normal flow.
3. **First setup or plugin upgrade only:** if Codex was already running, refresh its MCP tools in **Settings → Plugins → MCPs** (refresh icon beside Servers). Alternatively restart the client. Claude Desktop needs a complete quit and reopen after its configuration changes. Permit the tools when your client asks.
4. Chat in that client. Ask it to use **ai-architec-rhino** and read `get_context`. Rhino changes from waiting to **Connected · Codex/Claude Desktop** after actual authenticated protocol contact.
5. Ask for a model, inspect its preview in Rhino, then click **Apply to Rhino**. Ask the client to read `get_plan_status` and explain the confirmed result.

In **MCP · chat in your AI client** mode, Rhino has no second chat composer. It shows client replies, previews, results and an **Open Codex/Claude** button. Generate/Assist API options are hidden; the client supplies its plan. Local Analyze remains available. For direct chat in Rhino, switch to **Chat here · API / offline tools**, and configure your own API profile separately.

<img src="../rhino-plugin/docs/mcp-workspace-042.png" width="440" alt="Installed 0.4.2 MCP workspace with a real Codex box proposal awaiting human Apply">

Actual installed 0.4.2 workspace after a Codex proposal. This screenshot shows a preview awaiting Apply, not completed geometry.

### After disconnecting

Keep the AI client open. Click **Connect to Codex/Claude Desktop** again in Rhino. The bridge discovers the rotated session credential and reconnects automatically; **no client reload or configuration copy is required for Rhino MCP off/on**. A request made while disconnected returns an actionable error and does not terminate the bridge. Retry `get_context` after reconnecting. A timed-out modeling request is never automatically replayed: inspect the preview or request status before sending another model request.

The panel reports disabled, waiting, connected, current workflow stage and last contact. A client name is announced protocol metadata, not proof of account login. Heartbeats check the local connection every three seconds. A plugin upgrade changes the bridge path: run Connect again and refresh the client once to load the new executable.

### A first request

> Use ai-architec-rhino. Read get_context, then post_message with that document_id to confirm the connection. Propose one 1 x 2 x 3 meter box. Wait for my Apply in Rhino, then check get_plan_status and report the confirmed dimensions.

`post_message` displays a reply without changing geometry. `propose_plan` validates and previews. **Apply** executes native operations; **Cancel** leaves them unbuilt. **Ctrl+Z** in Rhino undoes an applied batch. The MCP server cannot execute arbitrary scripts or bypass Apply.

## Manual setup and diagnostics

Use the collapsed advanced section only when automatic registration is unavailable or you use another MCP client. **Copy configuration** is for the client's settings; **Copy connection-test prompt** is a message to send after the client has loaded the server. Pasting configuration into chat does not register it.

Codex registration uses the installed `codex mcp add` CLI and preserves other server entries. Claude setup merges only `mcpServers.ai-architec-rhino` into `%APPDATA%\Claude\claude_desktop_config.json`, preserving other settings and making a sibling `.architec-backup`. Invalid JSON is reported without overwriting it. No key or session token is exported. Both applications must be installed and signed in by their user; the plugin does not create accounts or log in.

For another compatible local STDIO client, use the installed `AIArchiTec.McpBridge.exe` and `ARCHITEC_MCP_PORT` from the panel's configuration. The executable uses the current Windows user's encrypted session descriptor. Replace old configurations containing `ARCHITEC_MCP_TOKEN`. Default port is 5188; a second Rhino session needs a different free port and a separate client entry. Port and endpoint are under advanced settings.

Official references: [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli), [Claude local MCP setup](https://modelcontextprotocol.io/docs/develop/connect-local-servers).

## ChatGPT in a browser

Opening a website does not register a local STDIO server. Browser/cloud ChatGPT requires a supported remote connector or Secure MCP Tunnel and account setup. This release does not provision a public tunnel, OAuth or browser-chat automation. Use the local Codex/Claude route above, or your personal API key for in-panel chat. [Official remote/private MCP guidance](https://developers.openai.com/api/docs/guides/tools-connectors-mcp)

## Workspaces and API profiles

- **Generate:** preview new geometry, then Apply. With the API route, set facade/layer options and attach supported references in Rhino. With MCP, describe and attach references in your AI client.
- **Analyze:** select objects or Recent output. Dimensions, Health and Layers are local read-only reports; **Run local audit** requires no API. This is not an engineering simulation.
- **Assist:** use selected/recent targets for Move, Scale or Extrude. In MCP, the client proposes the operation; in the API route, the panel has distance/axis/factor/height controls. Review, Apply and Undo work in Rhino.

**Save this profile** stores and activates an API profile. **Remember** controls encrypted persistence across Rhino restarts. **Test network** sends no key: HTTP 401 means the host answered, not that your key works. **Connect and discover models** checks authenticated access. Select a model and try a small request; live generation and vision quality depend on that account and model.
