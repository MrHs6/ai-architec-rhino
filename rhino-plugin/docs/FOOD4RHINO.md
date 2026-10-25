# Food4Rhino listing — 0.6.0-beta.9

Product: **AI Archi-Tec for Rhino**
Creator: **Hasan Sharif Askari**
Website: https://ai-arch.ir
Requirements: licensed Rhino 8.31+ within Rhino 8, Windows x64, .NET 8; Grasshopper for graph workflows.

## Short description

Native Rhino and editable Grasshopper workflows. Chat in your MCP client or use your own API in Rhino. Clear connections, readable chat, preview and Undo.

## Listing body

AI Archi-Tec connects architectural conversation to editable Rhino geometry and native Grasshopper definitions. Stay in your external AI client through MCP, or chat in Rhino using your own API account.

LATEST RELEASE: 0.6.0-beta.9

This update adds editable Grasshopper workflows, authenticated OpenRouter connection checks, clear saved/connected/error states, explicit model selection, reliable existing-object follow-ups and a readable scrolling workspace. Controls and options scroll with the conversation while the multiline composer stays fixed.

REQUIREMENTS

Windows x64, licensed Rhino 8.31 or later within Rhino 8, and the Rhino .NET 8 runtime. Grasshopper is required for graph workflows. One installer supports both Rhino and Grasshopper; no separate .gha is needed. macOS and Rhino 7 are not supported. No developer tools are required to install.

MCP: WORK IN YOUR AI CLIENT

Run archtec in Rhino, open MCP connection, select Codex or Claude Desktop and click Connect. Describe, plan, authorize native tool execution, inspect results and request edits in your client. The plugin is an execution bridge in this mode, with no second chatbot or mandatory local Apply click. First setup or an upgrade may need one client refresh; subsequent listener off/on reconnects use the existing bridge. Stop cancels pending work; completed batches remain undoable. Browser/cloud clients need separate compatible connector setup; opening a website does not connect it to localhost.

API: CONVERSATION INSIDE RHINO

Open API profiles, choose OpenRouter, OpenAI, Anthropic/Claude, Google Gemini, xAI/Grok or a compatible/local service, enter your own key and select Connect and discover models. Choose a compatible model explicitly. A saved profile is distinct from a connected session; an unsuccessful connection displays a useful error instead of silently using offline examples.

Use Generate, Analyze and Model Assist for new geometry, read-only inspection and edits to selected/recent objects. Review previews and Apply inside Rhino. Follow-up edits retain scoped object IDs. API credit and model access come from your provider; no shared key or credit is included. Optional remembered keys are encrypted for the current Windows user.

EDITABLE GRASSHOPPER DEFINITIONS

Run archtec in Rhino, not in Grasshopper component search. In API mode select Grasshopper - parametric definition; in MCP use the same Rhino connection. Discover supported built-in components, create sliders, typed inputs and wires, refine the same bridge-owned graph and bake only when explicitly requested. Graph Undo is in Grasshopper; baked-object Undo is in Rhino. Scripts, clusters, third-party libraries, file/network components, feedback cycles and replacement of unrelated or reopened graphs are excluded.

NATIVE MODELING AND VERIFIED RESULTS

Native operations include capped lofts, Brep booleans/join, sweeps, revolution, pipes, offsets, extrusion, validated native geometry payloads, closed manifold meshes, transformations and layer operations. Inspection reports measured dimensions, validity, closure, area and volume. Arbitrary model-generated scripts are not executed.

196 automated checks and 39 native layout checks pass. Real OpenRouter acceptance with the explicitly selected openai/gpt-4.1-mini covers Persian conversation, preview/Apply of a valid closed 2 x 4 x 3 meter solid (24 cubic meters), a follow-up move preserving its object ID, and 3DM save/reopen. MCP Grasshopper graph creation/editing/baking and the API graph path are also verified; the latter graph acceptance uses a mock planner. Other live provider accounts, vision quality, paid-provider graph generation and a second workstation remain unverified.

The gallery retains the actual MCP-built Azadi Tower reconstruction: 339 valid closed native Breps, open crossing passages, curved vault and stone details. It is a photo-based architectural reconstruction with assumed fine dimensions, not a surveyed/as-built or construction-ready model.

INSTALL OR UPDATE

1. Download the 0.6.0-beta.9 Windows installer ZIP and extract all files.
2. Save your work, close every Rhino window and run install.cmd beside the included Yak.
3. Reopen Rhino and type archtec.
4. Choose MCP in your external client or connect a personal API profile inside Rhino. For Grasshopper select that target; bake only on request.
5. Inspect the editable result and use native Undo when needed.

Download, release notes and SHA256:
https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.6.0-beta.9

Installation tutorial:
https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/INSTALLATION.en.md

Grasshopper guide:
https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/GRASSHOPPER.en.md

MCP setup and recovery:
https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/MCP-QUICKSTART.en.md

Actual validation and images:
https://github.com/MrHs6/ai-architec-rhino/blob/main/rhino-plugin/docs/VALIDATION.md

Support:
https://github.com/MrHs6/ai-architec-rhino/issues

This evaluation beta does not provide BIM, engineering simulation, construction documentation, unrestricted image reconstruction or a photorealistic render engine. Source inspection and binary evaluation follow the actual project license:
https://github.com/MrHs6/ai-architec-rhino/blob/main/LICENSE.md

Designed and developed by Hasan Sharif Askari. Website: https://ai-arch.ir

## Installer entry

- Title: **AI Archi-Tec 0.6.0-beta.9 — Windows installer**
- Platform: **Rhino > Windows > Version 8** and **Grasshopper > Windows > Version 8**
- File: ai-architec-rhino-0.6.0-beta.9-windows-installer.zip
- Description: Evaluation beta for licensed Rhino 8.31+ on Windows x64 (.NET 8). Includes Rhino and editable Grasshopper workflows, reliable API profiles and a readable scrolling workspace. Extract all files, close Rhino, run install.cmd, reopen Rhino and type archtec. MCP stays in your client; API uses preview/Apply. Release and checksums: https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.6.0-beta.9

## Current gallery

Keep the existing actual Azadi render and English workflow diagrams. Add api-controls-beta9.png and api-conversation-beta9.png to show the actual installed interface. The owner authorized replacing the public listing and installer with this release.
