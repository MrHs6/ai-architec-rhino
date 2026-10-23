# Food4Rhino listing — 0.5.0-beta.6

Product: **AI Archi-Tec for Rhino**
Creator: **Hasan Sharif Askari**
Website: https://ai-arch.ir
Requirements: licensed Rhino 8.31+ within Rhino 8, Windows x64, .NET 8 runtime.
Release: https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.5.0-beta.6

## Short description

Native Rhino modeling with your AI. Chat in Codex/Claude through MCP, or use your own API in Rhino. Editable solids, model inspection and Undo.

## Listing body

AI Archi-Tec connects architectural conversation to editable native Rhino geometry. Stay in your external AI client through MCP, or chat inside Rhino using your own API account. The two routes have distinct controls and histories.

VERSION AND REQUIREMENTS

0.5.0-beta.6 — public evaluation beta. Licensed Rhino 8.31 or later within Rhino 8, Windows x64 and the Rhino .NET 8 runtime. The ready-made installer needs no developer tools. macOS, Rhino 7 and Grasshopper graph authoring are outside this release.

MCP: STAY IN YOUR AI CLIENT

Open archtec in Rhino, choose MCP connection, select Codex or Claude Desktop, and click Connect. The per-user setup registers the installed local bridge. First setup or an upgrade may need one client refresh.

Describe, plan, authorize tool execution, inspect results and request edits in your AI client. Rhino validates and executes native operations and returns measured results. There is no second chatbot or mandatory Apply click in Rhino on the MCP route. Optional previews can be applied or cancelled from the client. The plugin does not run an autonomous AI planner in MCP mode.

Stop cancels pending work; completed batches remain undoable. Disconnect stops the Rhino listener. Enable connection restores it, and the existing client reconnects without copying configuration again. Request receipts prevent identical retries from duplicating a completed operation within the running panel session.

Codex has been tested with the installed plugin. Claude Desktop setup is provided; live Claude-account acceptance remains outstanding. Browser ChatGPT and cloud clients require a compatible connector and separate setup. Opening a website alone does not connect it to Windows localhost; this beta does not provision public tunnels or cloud-account authentication.

API: CONVERSATION INSIDE RHINO

Use Generate, Analyze and Model Assist with your own OpenAI, OpenRouter, Google Gemini, Anthropic, xAI/Grok or compatible/local provider profile. In this route, review the viewport preview and click Apply to Rhino. Continue refining selected or recent native objects, with measured result feedback and Rhino Undo.

Provider accounts, model access and API charges stay with your provider. No shared key or AI credit is included. Optional remembered keys use Windows DPAPI for the current Windows user. Profiles retain separate endpoints, models and storage preferences.

NATIVE MODELING TOOLS

The 24-action catalog includes capped NURBS lofts, Brep booleans and join, sweep, revolution, pipes, offsets, extrusion, validated native geometry payloads, closed manifold meshes, transformations, layers, visibility, selection and explicit object deletion. Object inspection reports dimensions, validity, closure, area and volume. Viewport capture supports visual review. Model-generated scripts and arbitrary Rhino commands are not executed.

BUILT WITH THIS SYSTEM: AZADI TOWER

The gallery includes a normal Rendered viewport capture of the final tower built in Rhino through AI Archi-Tec MCP, with Codex directing the tools. The editable result contains 339 valid closed native Breps: four flared stone piers, clear crossing passages, a curved turquoise vault, fitted stone lattice, facade inlays and crown details. The deliverable contains only the tower, without landscape.

The model uses a 63 x 42 meter base and 45 meter overall height. It is a photo-based architectural reconstruction with assumed fine dimensions, not a surveyed/as-built model. Read-only checks verified 84 clear passage samples and four positive stone-pier controls. Sampling does not certify continuous clearance, structure or construction readiness.

Editable model and modeling report:
https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/AZADI-MCP-DEMO.en.md

INSTALL IN A FEW STEPS

1. Download the 0.5.0-beta.6 Windows installer ZIP and extract all files.
2. Save your work, close every Rhino window, and run install.cmd beside the included Yak package.
3. Reopen Rhino and type archtec.
4. For MCP, Connect your desktop client and continue the conversation there. For API, save your personal profile, choose a model, and use the local preview/Apply workflow.
5. Inspect the native result. Use Rhino Undo when needed.

Installation and troubleshooting:
https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/INSTALLATION.en.md

MCP connection and recovery:
https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/MCP-QUICKSTART.en.md

Downloads, release notes and SHA256 checksums:
https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.5.0-beta.6

Support:
https://github.com/MrHs6/ai-architec-rhino/issues

Privacy:
https://github.com/MrHs6/ai-architec-rhino/blob/main/rhino-plugin/PRIVACY.md

VERIFICATION AND BETA SCOPE

144 automated checks pass. Installed-plugin acceptance covers Codex registration, native modeling, object inspection, failure/Undo recovery and listener off/on reconnect without client Reload. Live paid-provider/vision testing, a real Claude account and a second licensed workstation remain unverified.

This beta does not provide BIM, construction documentation, engineering simulation, unrestricted image reconstruction or a photorealistic render engine. Future AI output quality depends on the client, model, brief and geometry review. Gallery model images record actual Rhino geometry; diagrams explain the workflow. Earlier conceptual product illustrations are not evidence of generated geometry.

Created by Hasan Sharif Askari. Website: https://ai-arch.ir

Source is available for inspection. Beta binary evaluation and source rights follow the actual project license:
https://github.com/MrHs6/ai-architec-rhino/blob/main/LICENSE.md

## Installer entry

- Title: **AI Archi-Tec 0.5.0-beta.6 — Windows installer**
- Platform: **Rhino > Windows > Version 8**
- File: `ai-architec-rhino-0.5.0-beta.6-windows-installer.zip` from the verified GitHub CI release.
- Description: Public evaluation beta for licensed Rhino 8.31+ on Windows x64 (.NET 8). Extract all files, close Rhino, run install.cmd, reopen Rhino and type archtec. MCP stays in the external AI client without a local Apply click; API chat retains preview/Apply. Includes native CAD tools, reconnect recovery and checksums. Installation guide and release: https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.5.0-beta.6

## Current gallery files

- `azadi-tower-render.png`: normal Rendered Rhino capture of the final 339-solid MCP model.
- `azadi-front-050.png`: front view and open central passage.
- `azadi-vault-050.png`: curved vault detail.
- `installation-quickstart.png`, `modeling-workflow.png`, `connection-architecture.png`: current English diagrams from `docs/assets/`.
- The original product logo remains the project icon.
- Historical 0.4.2 MCP preview/Apply screenshots should not describe the current MCP workflow.

## Owner review before final Save

Check the version, installer, gallery, English text, requirements, categories and license. The last Food4Rhino Save/publication action remains with the owner. Preparing this form does not establish that listing changes are published. GitHub releases and Food4Rhino/Yak catalog publication are separate.
