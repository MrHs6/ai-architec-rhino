AI Archi-Tec for Rhino 8 — beta

- Native `archtec` command and Rhino Options page; `AIArchitect` remains supported.
- Modern Studio panel, per-user BYOK settings and encrypted optional key storage.
- Validated native geometry, semantic multi-floor concept buildings, preview, Apply and Undo.
- Authenticated localhost MCP with a bundled desktop stdio bridge.
- Per-user CMD/PowerShell installation, Yak package, checksums, GitHub build/draft-release workflow and GitLab Windows-runner template.

Requires Windows x64, licensed Rhino 8.31+ and Rhino's .NET 8 runtime. Each user configures their own API service or local MCP client. No shared API credentials are included.

Beta limits: live provider/vision trial and clean-start installation on another licensed PC are still required. Concept geometry is not construction/BIM output; arbitrary image reconstruction and automatic cloud-to-localhost connectivity are not implemented. Public Food4Rhino/Yak catalog publication is separate from a source-host release. See docs/VALIDATION.md and docs/DISTRIBUTION.md.
