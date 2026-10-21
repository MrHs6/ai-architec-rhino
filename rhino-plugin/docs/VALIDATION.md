# Validation and handover — 21 October 2026

## Implemented behavior

- Native Windows Rhino 8 panel opened by `archtec` / `AIArchitect` / `AIArchitectFloat`, with per-user preferences under Tools > Options > AI Archi-Tec.
- Modern WPF view hosted inside the Eto/Rhino panel, with separate presentation, workflow, provider, MCP and geometry code.
- User-supplied OpenRouter/OpenAI-compatible keys; live model selection; optional Windows DPAPI storage; no embedded key.
- Reference-image submission to confirmed vision-capable OpenRouter models.
- Structured plans, viewport wire preview, explicit Apply, native document execution and batch Undo.
- Ten primitive/read/edit tools plus a semantic `create_building` action that creates editable slabs, columns, façade panels, layers and floor/role metadata.
- Local MCP Streamable HTTP and a bundled stdio desktop bridge. No unrestricted code execution or remote approval bypass.

## Commands and environment

Installed Rhino: **8.31.26126.13431**, running **.NET 8.0.14**, meters in the active document. A workspace-local official **.NET SDK 8.0.426** was prepared because the PC initially had no SDK.

Commands used: `dotnet build AIArchiTec.sln -c Release -m:1 -p:UseSharedCompilation=false`, console acceptance tests, `Yak.exe build --platform win`, `Yak.exe install <local-package.yak>`, Rhino plugin loading API and `AIArchitect`. Build scripts contain the repeatable commands. The installable distribution is tagged **rh8_31-win** and should be advertised for Rhino 8.31 or later, not all earlier Rhino 8 releases.

## Results

- Final solution builds with **0 errors and 0 compiler warnings**.
- **30 automated core/provider/MCP checks** pass, using mocked provider replies. See `core-test-results.txt`.
- **19 Rhino geometry acceptance checks** pass in headless documents inside the already-running Rhino process. These cover preview without mutation; native solid box dimensions and volume; correct layer; stable GUID under movement; unaffected sentinel geometry; Undo; stale previews; locked objects; closed extrusion; invalid batches; curves; millimeter conversion.
- **4 building checks** pass: 46 native parts for a 15 × 20 m five-floor building; exact 16 m overall height; floor/role metadata including identifiable fifth-floor parts; native transparent glazing material.
- A real localhost MCP client initialized the plugin, read active context, proposed a box, observed no document geometry change before Apply, and received the confirmed created GUID after native panel Apply. The bundled stdio bridge returned a real MCP ping response.
- A five-floor building was generated through the Studio prompt → preview → Apply workflow in the active document, producing 46 native objects. The editable example is provided as `demo-building.3dm`.
- The plugin was installed through Rhino's Yak CLI successfully. The initial beta was upgraded during development; the final beta package must be selected for future installations.
- The complete solution also builds using the pinned RhinoCommon NuGet SDK without referencing the local Rhino installation: 0 errors and 0 warnings. Rhino's signed Eto adapter is resolved from the running host; upstream unsigned Eto.Wpf binaries are not shipped.
- The `archtec` alias was added to the already-running session and invoked successfully. The final assembly contains the native `archtec` command. Its new Options page was instantiated and activated inside Rhino; the expected Eto page and unchanged-preference Apply both succeeded. Registration in the Options tree on a fresh startup remains part of the licensed restart gate below.
- The versioned Yak payload passes the package-content validator. The extracted CMD installer locates Yak and its package from a path containing spaces. PowerShell validates prerequisites and the shipped SHA256, supports WhatIf without installation and rejects an incorrect checksum. Windows PowerShell's existing Restricted execution policy was left unchanged; CMD calls Yak directly.
- GitHub's expanded [Windows build run](https://github.com/MrHs6/ai-architec-rhino/actions/workflows/rhino-plugin.yml) passed: full native solution compilation, all 30 core checks, Yak/Windows installer creation and artifact upload on a hosted runner with no Rhino installation. The tag-only draft-release job was correctly skipped for this branch run; tagged draft creation itself has not been exercised. The GitLab Windows-runner template has not been run on a GitLab account.
- The extracted CMD installer performed a real per-user Yak installation of **0.2.0-beta.3** successfully, including from a directory containing spaces, with the existing PowerShell restriction unchanged.
- An additional authenticated MCP acceptance proposed moving all 10 fifth-floor components by 2 m along Y. After native panel Apply, all 46 GUIDs remained stable, fifth-floor bounds moved exactly as requested and all lower-floor bounds remained unchanged. Rhino Undo was then invoked to restore the demonstration model.

## Practical limits and outstanding validation

- No personal provider key was supplied. Live paid OpenRouter/OpenAI/Gemini/Claude responses have **not** been validated with the owner's account; transport, structured parsing, exact model IDs and failures were tested with mocks. Image interpretation quality still needs a real vision-model trial.
- An actual Claude Desktop/ChatGPT account was not connected. MCP protocol and local bridge were tested directly. A cloud chat cannot access Windows localhost without a local client/connector; opening the ChatGPT website does not configure MCP or send a prompt.
- Clean-start loading of the final installed package and a clean installation on a second licensed Windows/Rhino machine remain outstanding release checks.
- Concept buildings are editable architectural geometry, not BIM/construction documentation. Arbitrary reference reconstruction, free-form topology, Grasshopper, project-memory synchronization and automatic public-cloud hosting are not implemented.
- Public Food4Rhino/Yak publication was not performed. The package, listing draft and source are prepared for owner review.

## Milestones and next steps

| Milestone | Evidence |
|---|---|
| Native plugin + panel | Plugin load success; `AIArchitect` opens native panel |
| Context reader + safe executor | Core and Rhino acceptance checks |
| AI integration | BYOK settings; live model catalog client; provider mocks; live key trial remains |
| Preview / Apply / Undo | Direct UI/MCP acceptance and Rhino tests |
| Multi-floor modeling | Editable five-floor model and semantic metadata |
| MCP interoperability | Authenticated local initialize/tool calls + stdio bridge |
| Packaging | Built Yak; successful local install |
| Public release | Draft listing prepared; licensed restart, live AI trial and owner publishing remain |

The next concrete verification is to enter the owner's API key in Settings, connect a vision-capable model, and try one reference-guided building request. Then verify final-package startup and installation on a second licensed workstation.


