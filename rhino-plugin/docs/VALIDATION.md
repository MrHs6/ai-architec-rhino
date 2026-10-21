# Validation — AI Archi-Tec 0.3.0-beta.2

## Current revision

- Generate, Analyze and Model Assist have independent session drafts, reference attachments, histories and option controls. Switching cancels the single active viewport preview.
- Generate creates new geometry with facade/layer controls. Analyze is read-only and provides local dimensions, geometry validity/closure and layer reports. Assist offers selected/recent scopes and deterministic Move/Scale/Extrude previews.
- Connection overview, API profiles, MCP connection, Preferences and About are separate opaque pages. MCP starts explicitly. The supplied original logo is embedded and displayed at navigation, header and About sizes through layout clipping, without modifying the original artwork file.
- Six independent provider profiles: OpenRouter, OpenAI, xAI/Grok, Google Gemini, Anthropic/Claude and compatible/local. Native Gemini/Anthropic protocols, model pagination, headers, image formats and structured outputs were implemented from the official references linked in docs/PROVIDERS.md.

## Evidence

- **beta.2 clean-start repair verified on the owner's workstation:** Rhino was closed with an empty, unmodified document; four obsolete Yak versions and the manually loaded prototype binary package were archived with per-file SHA256 verification, then removed at the owner's request. Only `0.3.0-beta.2` remains in the package directory.
- On a genuinely new Rhino process, only `AIArchiTec.Studio` and `AIArchiTec.Contracts` from the installed beta.2 directory were loaded. The native `archtec` command had a nonempty command ID, opened the new panel, and the registered plugin owner was the same installed assembly. There was no legacy `AIArchiTec` assembly or duplicate-ID error.
- **19 workspace/profile/audit checks and 19 geometry checks passed in that fresh process**, rather than a replacement panel hosted by the old plugin. About reports the actual installed file; the native Options page instantiates successfully.
- The installer now refuses installation while Rhino is running. Both registration helpers are included; CMD uses native registry tooling, preserving operation when PowerShell script execution is restricted. Repeated registration repair after shutdown reports the already-correct target without creating another backup.

- Full Windows/.NET 8 native solution builds with zero compiler errors and warnings.
- **67 automated core/provider/MCP/workspace checks passed.** Provider requests and responses use mocks, not paid account credentials. Tests verify native request formats, image parts, pagination, exact catalog IDs, incomplete-output rejection, credential destinations, error redaction and mode/scope boundaries.
- **19 Rhino geometry regression checks passed again** in headless documents inside the existing Rhino process: preview isolation, exact box dimensions/volume, layers, stable GUIDs, movement, unaffected geometry, Undo, stale targets/units, locked targets, curves and capped extrusion.
- **20 native UI/audit/assist checks passed:** independent drafts/histories, different workspace controls, separate pages, provider-key/endpoint drafts, DPAPI roundtrip, actual dimensions/validity/layer audits and scoped Assist preview without native document changes.
- Further checks confirmed six providers in Rhino's Options page, a real authenticated local MCP context/proposal under the revised workspace policy, and two isolated persistence checks: Remember-off removes both stored key copies; saving one provider retains another provider's encrypted credential/model.
- Prior beta.1 UI acceptance used the user's original running panel while retaining its 46 demo objects and selection. That in-process replacement did not establish clean-start behavior; beta.2 acceptance above addresses the legacy startup conflict.
- Prior beta evidence includes four semantic-building checks and actual MCP Apply/Undo, including a fifth-floor-only edit with stable GUIDs and exact restoration. These remain regression context, not a new live-model trial.

## Remaining acceptance

- No real provider key was supplied for this revision. Live Grok/Gemini/Anthropic/OpenRouter/OpenAI requests and image quality still require the user's account-based trial. Passing mocks does not establish live account/model compatibility.
- Clean-start acceptance now passes on the owner's workstation. Installation on a second licensed Windows/Rhino workstation remains outstanding.
- A real external desktop AI account was not connected. Local MCP HTTP and the bridge protocol were tested directly; cloud-to-localhost access still needs a compatible local client.
- UI screenshots and local reports demonstrate real implemented behavior. Thermal, structural, regulatory or daylight analysis, unrestricted image reconstruction, BIM, Grasshopper and persistent project memory are not provided.
- Public GitHub release status is visible in Releases/Actions. Food4Rhino/Yak catalog publication is separate and has not been performed.
