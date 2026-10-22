# Validation — AI Archi-Tec 0.4.2-beta.1

## Evidence for 0.4.2-beta.1 (22 October 2026)

- Build: zero warnings/errors, 122 automated checks passed. Added real-process STDIO tests initialize/discover tools before Rhino starts, recover passively, survive three server/token rotations, preserve UTF-8, deliver messages once, avoid replaying rejected plans and close the authenticated session on EOF. Claude configuration merging preserves unrelated settings and rejects malformed roots.
- Installed all 10 package files and verified their SHA256 against the final Yak. Fresh Rhino 8.31 loaded the 0.4.2 RHP from its installed package path.
- Clicked the actual Connect to Codex button. It registered the new installed bridge using the official CLI and opened the client. Its Settings showed the correct 0.4.2 path and port, with other servers preserved. One initial desktop MCP refresh loaded the upgrade into this existing chat.
- Actual Codex tools read the empty Meters document, then Rhino's Disconnect button stopped the listener. get_context returned the new actionable offline tool error. Clicking Connect in Rhino restored get_context without any client reload or configuration copy. Both bridge process IDs (16784 and 21636) remained unchanged through that cycle.
- post_message returned to the panel. A real propose_plan from this chat produced request 87f0ca3d-8dad-46a1-b462-bba95e028722 for one 1 × 2 × 3 meter box in document 268435457. The user clicked Apply. Native get_plan_status returned applied, created GUID 80af25e6-a96e-44ad-8c31-48e019106c53, no modifications, Undo serial 2. get_context confirmed exactly one Brep with bounds [0,0,0]–[1,2,3] meters. Codex posted the measured confirmation back into Rhino.
- A second native off/on after Apply preserved the exact same single Brep GUID and bounds, and the original request remained applied. No model request was replayed and no duplicate was created; no manual client reload was used.
- The installed MCP workspace has no PromptInput composer, API connection label or Generate/Assist API option controls in its visible UI. It shows Chat in Codex, actual connection contact, Open Codex and the native preview/Apply card. Fixed labels and guides are English.
- The obsolete 0.4.1 package was archived with all 10 file hashes verified before removing only that version directory. New 0.4.2 is the sole active package version.
- Live Claude account, paid API/vision, second-workstation installation and Food4Rhino/Yak catalog submission remain untested or unpublished. Codex's live local tools are verified; this is not a claim that browser ChatGPT is automatically connected.

## Historical revision evidence
# Validation — AI Archi-Tec 0.4.1-beta.2

## Evidence for 0.4.1-beta.2 (22 October 2026)

- Solution build: zero warnings/errors; **94 automated checks passed**. New tests exercise authenticated initialization, session headers, announced-client identity, tool discovery, status polling, disconnect, token-free JSON/TOML exports, Windows DPAPI discovery and the real stdio executable.
- The installed `0.4.1-beta.2` RHP loaded in a fresh Rhino 8.31 process. The preceding local test packages were archived with per-file SHA256 verification before removal. Public 0.4.1-beta.1 was not released.
- **9 native MCP UI checks passed**: English workspace guides, separate Codex/Claude setup, honest web-connector guidance, Copy for MCP semantics, independent API status and separate client/stage/last-contact displays.
- **8 installed bridge-to-Rhino checks passed**: encrypted discovery, initialize, four tools, active empty document, a real post_message in the panel, awaiting-approval proposal, actual announced test-client name, cancelled proposal status and clean disconnect. No active geometry was created or modified; the acceptance client was explicitly named as a protocol test, not Claude/ChatGPT.
- **19 workspace/audit, 19 native geometry and 11 isolated profile checks passed again** on this installed revision. No personal profiles were changed.
- New UI captures show the real English interface. The MCP image uses a live local protocol-test handshake, not a signed-in Claude/Codex/ChatGPT account. Prior building images and the downloadable example remain limited offline geometry demonstrations.
- No paid API key was used. Live provider generation/vision and a real external AI account still need account-based acceptance. The web/tunnel path is documented, not provisioned or claimed to be connected.

## Prior revision record
## Current revision

- Generate, Analyze and Model Assist have independent session drafts, reference attachments, histories and option controls. Switching cancels the single active viewport preview.
- Generate creates new geometry with facade/layer controls. Analyze is read-only and provides local dimensions, geometry validity/closure and layer reports. Assist offers selected/recent scopes and deterministic Move/Scale/Extrude previews.
- Connection overview, API profiles, MCP connection, Preferences and About are separate opaque pages. MCP starts explicitly. The supplied original logo is embedded and displayed at navigation, header and About sizes through layout clipping, without modifying the original artwork file.
- Six independent provider profiles: OpenRouter, OpenAI, xAI/Grok, Google Gemini, Anthropic/Claude and compatible/local. Native Gemini/Anthropic protocols, model pagination, headers, image formats and structured outputs were implemented from the official references linked in docs/PROVIDERS.md.

## Evidence for 0.4.0-beta.1 (21 October 2026)

- The Windows/.NET 8 solution builds with zero compiler errors and warnings. **80 automated core/provider/MCP/workspace checks passed**; provider requests use mocks, not paid credentials.
- A fresh Rhino 8.31 process loaded the installed `0.4.0-beta.1` package and opened the native `archtec` panel. Only that package version remains in the active package directory.
- **19 workspace/profile/audit checks passed**: independent modes and pages, six providers, original embedded logo, real dimensions/health/layer audits and encrypted credential isolation.
- **19 geometry checks passed** in headless documents: preview isolation, exact dimensions and volume, stable GUIDs, unaffected targets, native Undo, stale targets/units, locked geometry, curves and capped extrusion.
- **11 profile synchronization checks passed** in isolated temporary settings with fixture credentials. Saving OpenAI makes it active; Remember-off clears both disk copies but retains a session key; other providers remain intact; save and network statuses are independent. Personal profiles were not changed.
- **9 conversational checks passed in the active empty document**, scheduled on Rhino Idle after the script command finished. The offline example creates 46 native parts; Apply reports measured 15 × 20 × 16 meter bounds. A Persian follow-up moves only 10 fifth-floor parts by +2 meters in world Y, preserves all GUIDs and leaves 36 parts unchanged. Undo restores positions and chat detects the change. The example remains available in `demo-building.3dm`.
- **Live connectivity from Rhino was verified without a key**: OpenRouter `/models` returned HTTP 200; OpenAI `/models` returned HTTP 401. On the owner's workstation, one explicit outbound Windows Firewall block for Rhino.exe caused socket error 10013. The owner authorized disabling only that exact rule; Domain, Private and Public firewall profiles remain enabled. This workstation repair is not part of the installer or plugin.
- The first diagnostic script incorrectly blocked Rhino's UI thread while awaiting network I/O; the empty test process was restarted, and the successful probe ran on a worker task. The production connection UI uses asynchronous event handlers and cancellation.
- New captures are actual installed WPF views and the native Rhino viewport. The demo is a limited offline example, not proof of live AI model quality.

## Earlier startup repair

The preceding 0.3.0-beta.2 acceptance established that the empty gray panel and unknown `archtec` command came from legacy manual registration. Obsolete versions and the prototype package were archived and SHA256-verified before removal. A clean process loaded the installed Studio/Contracts assemblies, the native Options page and command worked, and 19 workspace plus 19 geometry checks passed. Installers now require Rhino to be closed and repair only this plugin's registration with a per-user backup. That repair is retained in 0.4; the evidence above is the new revision's test record.
## Remaining acceptance

- No real provider key was supplied for this revision. Live Grok/Gemini/Anthropic/OpenRouter/OpenAI requests and image quality still require the user's account-based trial. Passing mocks does not establish live account/model compatibility.
- Clean-start acceptance now passes on the owner's workstation. Installation on a second licensed Windows/Rhino workstation remains outstanding.
- A real external desktop AI account was not connected. Local MCP HTTP and the bridge protocol were tested directly; cloud-to-localhost access still needs a compatible local client.
- UI screenshots and local reports demonstrate real implemented behavior. Thermal, structural, regulatory or daylight analysis, unrestricted image reconstruction, BIM, Grasshopper and persistent project memory are not provided.
- Public GitHub release status is visible in Releases/Actions. Food4Rhino/Yak catalog publication is separate and has not been performed.
