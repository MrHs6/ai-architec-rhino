# AI Archi-Tec for Rhino 8

Version 0.4.1 adds an English MCP onboarding interface, client-announced connection stages and token-free Claude/Codex setup exports. Read the [MCP setup and chat guide](MCP-QUICKSTART.en.md) before connecting a client. A running server or an open ChatGPT website is not a confirmed AI account connection.

[فارسی](../README.md) · [Downloads](https://github.com/MrHs6/ai-architec-rhino/releases) · [Report an issue](https://github.com/MrHs6/ai-architec-rhino/issues)

An architectural modeling assistant that runs inside Rhino. Open Studio with **`archtec`**, describe a concept, inspect the proposed geometry in the current document, then **Apply to Rhino**. Use your own API account or connect a compatible desktop AI client through localhost MCP.

**Current release: 0.4.1-beta.2. Windows x64, licensed Rhino 8.31+ within Rhino 8, .NET 8 runtime.** This is a concept-modeling beta; live paid-provider/vision trials and clean installation on a second licensed workstation remain outstanding.

## Install

1. Download the [Windows installer ZIP](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.4.1-beta.2/ai-architec-rhino-0.4.1-beta.2-windows-installer.zip) from the [release page](https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.4.1-beta.2).
2. Extract all files, keeping `install.cmd` next to the `.yak` package. Save your work and close all Rhino windows before running `install.cmd`; it invokes Rhino's installed Yak tool for the current Windows user.
3. Reopen Rhino.
4. Type **`archtec`** and press Enter.
5. Open **API profiles**, choose your API source, enter your personal key, load compatible models and select a model. Alternatively start **MCP** and configure a compatible local desktop client.

No Visual Studio, Node or SDK is needed for the ready-made installer. API access and billing belong to each user's provider account; no shared key or AI credit is included.

Alternatively [download the Yak directly](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.4.1-beta.2/ai-architec-rhino-0.4.1-beta.2-rh8_31-win.yak), drag it onto Rhino, or run:

```cmd
"%ProgramFiles%\Rhino 8\System\Yak.exe" install "D:\Downloads\ai-architec-rhino-0.4.1-beta.2-rh8_31-win.yak"
```

Adjust the path. Restart after installation. CMD calls Yak and its native registration-repair helper without altering PowerShell execution policy. Legacy prototype users should use the ZIP installer because Yak alone does not repair manual registration. For a custom Rhino installation, set `ARCHITEC_RHINO_SYSTEM` to its System folder before running `install.cmd`. `install.cmd /check` only verifies local paths. `install.ps1 -CheckOnly` also checks Rhino version, package identity and the accompanying checksum when PowerShell scripts are allowed by your device policy.

Preferences: **Tools > Options > AI Archi-Tec**. Plugin management: **Tools > Options > Plug-ins**. `AIArchitect` is an alternative command; `AIArchitectFloat` opens a floating panel. To uninstall, use PackageManager or `Yak.exe uninstall ai-architec-rhino`, then restart. Optional per-user API preferences are retained.

## Screenshots and example

<img src="../rhino-plugin/docs/studio-panel.png" width="500" alt="Actual AI Archi-Tec panel with the offline building example">

![Editable native building geometry](../rhino-plugin/docs/building-output.png)

The screenshots show the real limited offline building example, not an unverified online-AI result. [Download the editable 3DM](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.4.1-beta.2/demo-building.3dm): 46 native parts for a five-floor concept building.

## Features and workflow

- Dockable native Studio with independent Generate, Analyze and Model Assist drafts, references, histories and options.
- Separate OpenRouter, OpenAI, xAI/Grok, Google Gemini, Anthropic/Claude and compatible/local profiles. Native Gemini and Anthropic protocols, fetched catalogs and exact selected model IDs. [Protocol details](PROVIDERS.md)
- Native Gemini image parts; OpenRouter/xAI/Anthropic images follow model capabilities. Interpretation quality still needs live testing.
- Editable rectangular multi-floor concept buildings with separate slabs, columns, facade panels, layers and floor/role metadata.
- Boxes, lines, polylines, curves, extrusion, movement, scaling, selection and property reading.
- Structured-plan validation, wire preview, explicit Apply, and native Rhino batch Undo. Arbitrary model-generated code is not executed.
- Authenticated localhost MCP and a bundled stdio desktop bridge.
- Independent per-user settings and optional Windows DPAPI key persistence; no embedded API key.

Local Analyze audits measure dimensions, geometry validity/closure and layers. Model Assist offers deterministic Move/Scale/Extrude controls with selected/recent target scopes. Connection overview, API profiles, MCP connection, Preferences and About the creator are separate pages. The supplied full-resolution logo is used in the header, navigation and About page.

**Prompt → API or local MCP client → validated plan → viewport preview → Apply → native objects.** Generate alone does not modify the document. Inspect the preview before Apply; use Ctrl+Z in Rhino to undo. Analyze reads limited context and does not propose geometry changes.

For a supported example without an API key, select Offline examples and try:

```text
Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights and a glazed facade.
```

The offline parser is limited to numeric building/box/move examples. Free-form natural-language planning requires a compatible online model.

## Local MCP

Start MCP in Studio. Default endpoint: `http://127.0.0.1:5188/mcp`. Use **Copy desktop client configuration** for the installed stdio bridge path and current local bearer token. Configure that JSON in your compatible desktop client's MCP settings; reload the client as required.

Tools: `get_context`, `propose_plan`, `get_plan_status`. Read the current `document_id` before proposing a plan. Proposals only preview; Apply in Rhino executes. Tokens rotate when the server restarts. Do not publish the copied client configuration.

A cloud service cannot reach your Windows localhost directly. A compatible local client/bridge is required; this package does not configure public tunnels or ChatGPT OAuth connectors. Opening the ChatGPT website does not connect MCP or submit a prompt. Actual desktop-account interoperability has not been confirmed for every client.

## Beta status

67 automated checks, a rerun of 19 native Rhino geometry checks, and 20 native workspace/profile/audit/assist checks passed. Four building checks were recorded in the prior beta. A real MCP proposal/Apply, fifth-floor-only edit, stable GUIDs and exact Undo restoration were verified. The complete native solution and release packaging also passed on a hosted Windows runner. [Validation report](../rhino-plugin/docs/VALIDATION.md)

Live paid-provider responses, vision interpretation quality and clean startup on a second licensed machine still require validation. Concept geometry is not BIM or construction documentation. Arbitrary image reconstruction, unrestricted building generation, Grasshopper, persistent project memory and cloud hosting are not implemented. Food4Rhino/Yak catalog publication is separate from this GitHub release.

## Build and support

```powershell
git clone https://github.com/MrHs6/ai-architec-rhino.git
cd ai-architec-rhino\rhino-plugin
.\scripts\build.ps1 -UseNuGet
.\scripts\package.ps1
```

Build with Windows and .NET SDK 8. The pinned official RhinoCommon SDK is restored from NuGet; local Rhino is needed to run the plugin, not to compile it. CI builds and uploads installer artifacts; matching version tags create draft prereleases for maintainer publication. A GitLab Windows-runner template is included. [Distribution guide](../rhino-plugin/docs/DISTRIBUTION.md)

Share the project: **https://github.com/MrHs6/ai-architec-rhino**. For issues, include Rhino/plugin versions, reproduction steps and the error text; omit keys and tokens. [Privacy](../rhino-plugin/PRIVACY.md) · [License status](../LICENSE.md)

Created and owned by **Hasan Sharif Askari**. Source is available for inspection; an open-source modification/redistribution license has not been selected.
