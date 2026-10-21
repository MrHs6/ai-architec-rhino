AI Archi-Tec 0.3.0-beta.2 — repair legacy plugin registration

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

67 automated checks and native Rhino workspace/audit/geometry acceptance passed. Live paid-provider/vision testing and clean-start acceptance on a second licensed workstation remain outstanding.

Save your work, close all Rhino windows, install the Windows ZIP, reopen Rhino and run archtec. Legacy prototype users should use the ZIP because Yak alone does not repair manual registration. Configure your own service under API profiles. This remains a concept-modeling beta; no shared key, BIM or automatic public-cloud connector is included.
