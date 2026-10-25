AI Archi-Tec 0.6.0-beta.9 candidate — reliable API conversations and a readable workspace

- Authenticate OpenRouter keys with the official `/key` endpoint before reading its public `/models` catalog. Reject management/provisioning credentials for inference; keep compatible text models that advertise structured output without requiring unused tool calling.
- Distinguish a saved profile, an active API connection and a failed connection. A saved or failed API profile no longer silently sends its conversation to the limited offline planner; local examples require the user's explicit offline choice.
- Preserve the selected model ID. If it disappears from the refreshed catalog, require an explicit model choice instead of substituting the first entry. Hide stale model choices while disconnected and keep the dashboard state consistent across workspace/MCP route changes.
- Cancel an active request before disconnecting. Retain API failure diagnostics and clear a failed provider instance consistently. API conversation histories remain separate from the external-client MCP execution log.
- Recognize an explicit edit even when its instruction says “do not create a replacement.” Follow-up moves stay scoped to the existing native object instead of entering Generate with no target ID.
- Keep a compact brand and bounded multiline composer fixed while connection controls, workspace options, recent results, the conversation and preview share one vertical scrolling area. Upper menus can scroll away; the conversation is no longer squeezed into a small independent viewport. Preserve Generate, Analyze, Assist, Grasshopper and external-client MCP routes.
- Increase conversation/supporting text sizes, render selected model names through the custom ComboBox template, and route the closed selector's mouse wheel to its page instead of accidentally changing models. Keep native recent-result controls hidden in MCP and Grasshopper views.
- Classify bounded API errors into credential, credit/budget, permission/region, rate, routing/schema and network-gateway failures, including errors carried in HTTP 200 responses. Never display raw provider bodies, credentials, account identifiers or prompts in these diagnostics; no model fallback or automatic modeling replay is introduced.
- Explain the .NET 8 process-wide Windows proxy snapshot: after an OS network/proxy change, save work and restart Rhino before reconnecting. The plug-in preserves current network settings and TLS validation; this step does not guarantee removal of provider/network restrictions.
- Verify **196 automated core/protocol checks**, including authenticated discovery ordering, schema-only model compatibility, safe error handling, profile behavior and negated-creation follow-up instructions. Add explicitly scheduled native runtime fixtures for the paid real-provider conversation → preview → Apply → inspect loop, an existing-failure/no-silent-offline path that sends no new cloud requests, and detached WPF layout checks at normal, short and narrow sizes.

Installed 0.6.0-beta.9 passed 39 native WPF layout checks across 560 x 780, 420 x 640 and 350 x 540 panels. Real OpenRouter acceptance with the explicitly selected `openai/gpt-4.1-mini` passed authenticated discovery (389 compatible models at test time), Persian conversation without geometry, preview isolation, Apply of a valid closed 2 x 4 x 3 meter solid (24 cubic meters), follow-up translation by +3 meters along X preserving the object ID, and 3DM save/reopen. Nine native live-flow checkpoints passed; no mock or custom transport was used. Other providers retain protocol-fixture coverage and have not been live-tested with personal accounts in this revision. Public GitHub/Food4Rhino release publication remains separate.

---

AI Archi-Tec 0.6.0-beta.6 candidate — editable Grasshopper through MCP and API

- Add native component discovery, graph inspection and a validated `grasshopper_definition` action. Create editable components, sliders, typed input branches and wires; update an open bridge-owned graph in place. Bake is explicit.
- Keep MCP conversation and authorization in the external client. Add the Grasshopper target to Studio API with separate conversation history, graph-plan review and Apply. The solver runs only on commit.
- Load the installed Grasshopper editor through its official automation interface. Support zero-based RuntimeID, enable detached candidate solving and convert native geometric outputs for baking.
- Validate graph topology/ports/inputs, reject stale updates and scripts/third-party libraries, and roll back partial bakes. Graph Undo/Redo and Rhino bake Undo are independent.
- Verify 165 automated tests; real installed MCP create/update/idempotency/solid inspection; 19 in-Rhino API workflow checks using a mock planner, including Undo/Redo, rollback and `.gh` export/reopen. Paid providers and a second workstation remain untested.
- Compile against pinned official RhinoCommon/Grasshopper SDKs; keep host SDK assemblies out of the ten-file package. Add English/Persian Grasshopper setup and scope guides.

This candidate is prepared for evaluation; existing published 0.5 download links and Food4Rhino publication are not changed automatically.

---

AI Archi-Tec 0.5.0-beta.6 — client-controlled MCP and native CAD execution

- Keep MCP conversation, planning, tool approval, execution, inspection and follow-ups in the external AI client. Hide local API/chat/commit controls on the MCP route. API mode remains a separate in-Rhino conversation with preview and Apply.
- Add execute_plan with UUID receipts and client-managed optional previews. No mandatory Apply click in Rhino. Identical retries return the recorded receipt within the running panel session; conflicting request IDs are rejected.
- Expose 24 bounded native actions: capped lofts, booleans/join, sweep, revolution, pipe, offset, extrusion, manifold mesh, validated native geometry payloads, transforms, layers, visibility, selection and explicit object deletion. No arbitrary script or command execution.
- Add object validity/closure/mass properties, bounded point-containment probes, correct camera/target framing and actual viewport PNG capture.
- Keep **Enable connection** visible while disconnected, so restarting an existing connection does not repeat client registration. Prefer the desktop-managed Codex CLI over inherited PATH shims and show filtered, actionable setup diagnostics.
- Keep STDIO input responsive to Stop; forward MCP cancellation and check between native operations. Reconnect without replaying uncertain writes. Completed batches remain undoable.
- Fix explicit deletion of hidden construction, distinguish source reads from target mutations, and close Rhino's modeless rollback Undo scope so a failed batch does not block the next operation.
- Separate MCP execution logs from API histories. Dispose native panel resources on shutdown. Give Codex setup complete redirected input/output streams when launched from Rhino's GUI process.
- Replace the rejected Azadi development trial with a tower-only reference reconstruction: 339 valid closed Breps, a curved open cross vault, fitted stone lattice, pointed crown details and facade inlays. No landscape or hidden construction remains in the clean 3DM.

144 automated checks pass. See VALIDATION.md for installed-plugin acceptance and remaining limits. Paid-provider/vision accounts, a real Claude account and a second licensed workstation remain unverified. There is no unrestricted RhinoCommon, BIM or public-cloud connector in this beta.

Save documents, close Rhino, install the Windows ZIP, reopen Rhino and run archtec. After an upgrade, run Connect and refresh the AI client's MCP tools once for the new bridge path. Subsequent listener off/on reconnects do not require repeated copying.

Previous release record:

AI Archi-Tec 0.4.2-beta.1 — simpler MCP setup and automatic reconnect

- Connect to Codex/Claude Desktop enables localhost MCP, registers the installed bridge for the current Windows user, and opens the client when available. Other clients retain manual setup under advanced diagnostics.
- Chat in your AI client. The MCP workspace hides the Rhino composer and API modeling options; it displays replies, previews, results and Open client. API chat remains a separate route.
- Keep the client open when Rhino MCP is switched off/on. The bridge stays alive, discovers the new credential/session and reconnects automatically. First setup or an upgrade still needs one client refresh when the old executable is already loaded.
- Return a useful error while Rhino is offline. Reconnect pre-dispatch authentication/session rejections once, but never replay timeouts, ambiguous failures or tool errors.
- Preserve UTF-8 prompts and responses, including Persian and multiplication symbols. Prune stale client status and show actual authenticated client contact rather than assuming account login.
- Add 28 checks to the existing 94: isolated configuration merging, real STDIO discovery while offline, passive recovery, three credential rotations with one open process, exactly-once messages and rejected plans, and clean shutdown.

122 automated checks pass. Native installed Codex acceptance and remaining limitations are recorded in VALIDATION.md. No paid API/vision account, live Claude account or second Windows workstation was used to validate this revision. Food4Rhino/Yak catalog submission remains separate from the GitHub release.

Previous release record:

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
