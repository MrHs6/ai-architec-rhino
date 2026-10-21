# MCP connection and chat

The product interface and in-panel guidance are English. Persian prompts remain supported.

## Choose where you chat

- **Chat here · API / offline tools:** connect your provider, key and model under API profiles. Without a connected API, this route offers limited geometry examples and local audits. Starting MCP does not connect this input to a paid AI account.
- **MCP client · copy request to Claude / Codex:** chat in a configured local client. The panel's **Copy for MCP** button copies your request; paste and send it in that client. Replies can return through `post_message`; geometry proposals return through `propose_plan`. Images must be attached in the client. Sending to a browser tab is not automatic.

## Connect a client

1. Open Rhino, run `archtec`, open **MCP connection**, and start the server. It initially says **waiting for a client**.
2. Select **Claude Desktop** or **Codex**, then copy its setup. The setup contains the installed bridge path and port, without a token. Add it to the application's MCP configuration, preserving other entries. Pasting configuration into a chat does not register a server.
3. Claude Desktop: **Settings → Developer → Edit Config**, then merge the `ai-architec-rhino` entry into `mcpServers` in `%APPDATA%\Claude\claude_desktop_config.json`. Fully quit and reopen Claude. [Official local-client instructions](https://modelcontextprotocol.io/docs/develop/connect-local-servers)
4. Codex: add a **STDIO** server in the client's MCP settings using the copied command and `ARCHITEC_MCP_PORT`, or merge the TOML into `~/.codex/config.toml`. Reload the connection. CLI users can check configuration with `codex mcp list` and active tools with `/mcp`. Menu labels depend on client version. [Official MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)
5. Copy and send the **connection-test prompt** in the configured client. It must use **ai-architec-rhino**, rather than another server such as `rhino-mcp-router`.
6. Watch the panel for the announced client name, handshake, tool discovery, context, proposal and execution stages. Client names are declared protocol metadata; they do not verify a user's account identity.

The bridge reads the current session credential through Windows CurrentUser DPAPI discovery. After restarting the server, **reload the client connection** to start a new bridge; no fresh token needs to be pasted. Replace legacy `ARCHITEC_MCP_TOKEN` configurations with the new setup. If an upgrade changes the installed bridge path, copy the new version's setup again.

## Check conversation and geometry

Start with:

> Use only ai-architec-rhino. Call get_context, then post_message with that document_id to confirm the connection in the generate workspace. Do not change geometry.

Then request a small preview:

> Propose a 1 x 2 x 3 meter box. Wait for me to Apply in Rhino, then read get_plan_status and use post_message to explain the confirmed result.

`post_message` displays a response without changing geometry. `propose_plan` validates and previews. **Apply in Rhino** performs the native operation; **Cancel** leaves the proposed geometry unbuilt. `get_plan_status` returns the result for that request. Existing selection/recent-result scope checks remain enforced.

| Display | Meaning |
|---|---|
| Server stopped | Start the local server |
| Waiting for a client | No initialize handshake received |
| Handshake / tools shared | A client initialized and discovered tools |
| Document context returned | get_context completed |
| Processing / message delivered | An actual tool request or chat reply arrived |
| Preview ready | Awaiting Apply or Cancel in Rhino |
| Changes confirmed | Native Rhino execution completed |
| No recent contact / session closed | Last observed activity or explicit bridge shutdown; continuous connection is not assumed |

The local bridge sends a heartbeat every 15 seconds. A native HTTP client without heartbeats may remain open while the panel reports no recent contact. Session activity contains stages and tool names, not model prompts, keys or geometry.

## Workspace guide

- **Generate:** creates new geometry using the facade and layer options; review the preview and Apply.
- **Analyze:** select objects or Recent output. Dimensions, Health and Layers report scoped geometry. **Run local audit** needs no API and never modifies geometry. This is not an engineering simulation.
- **Assist:** edits selection or recent output. Move uses distance and axis; Scale uses a factor (2 doubles size, 0.5 halves it); Extrude uses height on suitable curves. Preview, Apply, and use Rhino Ctrl+Z to undo.

## ChatGPT web

Opening ChatGPT does not connect localhost or import a STDIO configuration. Supported products can use a remote MCP connection or **Secure MCP Tunnel**, with the required account configuration and capabilities. This release does not provision that tunnel, OAuth, or automatic browser-chat sending. Use the local client route above, or a personal API key for direct chat in the Rhino panel. [Official remote/private MCP guidance](https://developers.openai.com/api/docs/guides/tools-connectors-mcp)

## API acceptance

**Save this profile** confirms local storage and activation. **Remember** controls encrypted persistence across Rhino restarts. **Test network** sends no key: HTTP 401 means the host answered, not that your key works. **Connect and discover models** checks authenticated access and discovers models. Select a model and test a small request. Live generation and image quality depend on that account and selected model; mocked tests or no-key probes cannot establish them.
