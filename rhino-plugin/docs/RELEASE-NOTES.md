AI Archi-Tec 0.4.1-beta.2 — English connection guidance and verified MCP stages

- Keep all fixed interface labels, in-panel guides and connection statuses in English. Persian user prompts and language-aware conversation feedback remain supported.
- Distinguish a listening server from a client handshake, tool discovery, context read, message, preview, Apply and disconnect. Show the client's announced name and last contact without claiming account authentication.
- Export Claude Desktop JSON and Codex TOML separately. The bridge reads the current session token from a Windows CurrentUser DPAPI descriptor; setup exports no longer contain tokens. Reload the client after restarting MCP.
- Add a no-geometry post_message tool for client replies. The MCP chat route explicitly copies a request to the external client; direct panel chat continues to use a personal API. Opening ChatGPT does not configure an account connector.
- Add an English workspace guide for Generate, Analyze, Move, Scale and Extrude, plus English/Persian setup documents explaining local clients and the separate web/tunnel route.
- Correct the Assist classification of MCP extrusion plans.

94 automated checks pass, including a real stdio bridge handshake and DPAPI discovery. Native installed-plugin acceptance verifies chat delivery, tracked client name, preview isolation and cancellation. No live Claude/ChatGPT account or paid API generation was tested.

Previous 0.4.0 changes:

- Save and activate the selected API profile before model discovery. A failed connection retains the profile, endpoint and selected model.
- Show session-only credentials, encrypted saved credentials and network status separately. Remember-off keeps the key only until Rhino exits; switching providers preserves independent drafts and profiles.
- Add a no-key network diagnostic, cancellation and distinct socket/DNS/TLS failure messages. The plugin does not change firewall settings.
- Use the OpenAI Responses API with strict action-plan output and `store: false` for direct OpenAI connections. Other providers retain their native protocols.
- Report actual object counts, measured bounds and floor metadata after Apply, in the request language, without an additional paid request.
- Refine the recent result using the same object GUIDs. Explicit edit follow-ups enter Assist; histories and recent targets stay separate for each open document during the current session.
- Detect Undo, deletion and manual geometry changes and refresh the measured chat feedback.
- Document the four reference workflows and provide an accurate Food4Rhino listing draft and a Persian step-by-step conversation tutorial.

Includes the preceding startup repair:

- Fix the gray, empty prototype panel returning after a Rhino restart: installers now repair this plugin's legacy manual registration after Yak installation, with a per-user registry backup.
- Register the `archtec` command when upgrading the prototype. Existing load protection and personal API settings are preserved.
- Show the actual loaded assembly version and location in About and the Rhino startup log.
- Installer reports a registration-repair failure instead of claiming a complete upgrade.
- Installation now requires all Rhino windows to be closed so a running prototype cannot overwrite the repaired registration during shutdown.

Includes the preceding workspace/provider improvements:

- Generate, Analyze and Model Assist now have separate options, drafts, reference attachments and conversation histories.
- Local Analyze reports actual model dimensions, geometry health and layers. Assist has selected/recent scopes with Move, Scale and Extrude previews; Generate has facade and layer controls.
- Separate API profiles, MCP connection, connection overview, Preferences and About pages; opening MCP no longer starts the server.
- Original supplied logo at readable header/navigation/About sizes; creator and workflow details for Hasan Sharif Askari.
- Independent OpenRouter, OpenAI, xAI/Grok, Google Gemini, Anthropic/Claude and compatible/local profiles, with native Gemini and Anthropic protocols, model discovery/pagination and image payloads.
- Profile keys are optionally DPAPI-encrypted. Remember-off clears both saved copies; changing one profile preserves other providers.

80 automated checks and 11 native profile synchronization checks passed. Native conversation acceptance and live-network results are recorded separately in VALIDATION.md. Paid-provider/vision testing and installation on a second licensed workstation remain outstanding.

Save your work, close all Rhino windows, install the Windows ZIP, reopen Rhino and run archtec. Legacy prototype users should use the ZIP because Yak alone does not repair manual registration. Configure your own service under API profiles. This remains a concept-modeling beta; no shared key, BIM or automatic public-cloud connector is included.
