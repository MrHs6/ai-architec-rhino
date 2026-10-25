# Provider protocols and independent profiles

Each service has its own endpoint, credential, remembered-key choice and selected model. Switching services preserves unsaved profile drafts in memory. **Save this profile** persists preferences and selects that profile; it does not establish a live API connection. Keys are persisted only when Remember is enabled, using Windows DPAPI; otherwise the key remains available in this Rhino session, including the native Options page, and is cleared at exit. Connect saves first, authenticates where supported, then fetches the catalog. A failed network request does not discard the saved profile. Settings files are replaced atomically. Saving or discovering models does not send a modeling request.

| Service | Discovery | Generation | Authentication |
|---|---|---|---|
| OpenRouter | Authenticate with `/api/v1/key`, then `/api/v1/models`; require `structured_outputs` | OpenAI Chat Completions + JSON schema | Bearer header |
| OpenAI | `/v1/models`, exclude known non-text modalities | Responses + `text.format` JSON schema, `store:false` | Bearer header |
| xAI / Grok | `/v1/language-models`, text output + image metadata | Chat Completions + JSON schema | Bearer header |
| Google Gemini | `/v1beta/models`, paginated; Gemini text generation | Native `models/<id>:generateContent`, structured generation config | `x-goog-api-key` header |
| Anthropic / Claude | `/v1/models`, paginated; capability metadata | Native `/v1/messages`, `output_config.format` | `x-api-key`, `anthropic-version` headers |
| OpenAI-compatible / local | `/models` on user-specified base | `/chat/completions` + JSON schema | Bearer header |

Gemini sends reference images as inline image parts. Anthropic sends base64 image content blocks; xAI and OpenRouter use image URL parts according to catalog image capabilities. Generic/OpenAI discovery does not confirm vision and image submission remains disabled there. Unsupported model/schema combinations fail explicitly; no silent provider fallback is attempted. Built-in Gemini, Anthropic and xAI credentials are restricted to their official API base URLs. Custom endpoints belong in the compatible/local profile.

The local transport tests use simulated official response shapes: credentials in headers, native payloads, model filtering/pagination, images, incomplete output and errors. **Live account-based acceptance still requires the user's own API keys.** These tests do not establish live provider availability or image interpretation quality.

## API connection troubleshooting

1. Select the service that issued the key and keep its official base URL. A key for OpenRouter is not a direct Anthropic or OpenAI key, even when the chosen OpenRouter model comes from that company.
2. **Saved** means the profile is retained. **Connected** means authentication/discovery completed for the current session. **Test network** sends no key and checks host reachability only; HTTP 401/403 in that test does not validate an account.
3. Use **Connect and discover models**, choose an exact catalog model and return to the workspace. For OpenRouter, the plug-in first checks the authenticated `/key` endpoint; its public `/models` endpoint alone cannot establish key validity. Management/provisioning keys are rejected for inference.
4. OpenRouter models must advertise structured output and text generation; tool-calling support is not required because the plug-in requests JSON schema plans. Catalog success does not guarantee account credit or that every endpoint can execute the selected schema. Unsupported combinations fail explicitly; no alternate model is silently selected.
5. Start with a short chat request, then a small modeling request. Inspect the preview and use **Apply** for geometry. Conversation-only responses need no Apply. Follow-up edits use the current document's selected or recent targets.

Diagnostics show a bounded category and HTTP status, without displaying raw provider bodies, prompts, account identifiers or API keys:

| Diagnostic | What to check |
|---|---|
| Credential rejected / 401 | Correct service, key enabled state and account permissions |
| Credit/quota / 402, spending or guardrail budget | API credit, key/workspace budget and reset window; a chat subscription is separate |
| Permission/policy / 403 | Key permissions, provider policy or guardrail; 403 alone does not prove an invalid key |
| Network/security-policy gateway or HTML page | API reachability from Rhino, existing network settings and the process restart note below |
| Region restriction | Provider availability and account location policy; the plug-in cannot remove a restriction |
| Rate limit / 429 | Provider reset window or account/model quota |
| Routing or data policy | Account routing/data-policy requirements; explicitly choose a compatible model if appropriate |
| Required feature / 400 or 422 | Text model with JSON schema support and provider protocol compatibility |

**Windows proxy changes while Rhino is open:** Rhino's .NET 8 runtime can keep the Windows proxy configuration captured when HTTP networking first initializes in that process. Creating another API client does not refresh that process-wide snapshot. If Windows network/proxy settings changed after Rhino opened, save the document, close Rhino normally, reopen it and reconnect. Keep the existing OS network configuration and TLS validation. This is a documented runtime behavior and a troubleshooting step, not a guarantee that a provider or network access restriction will disappear. The plug-in does not change proxy/VPN settings or replay failed modeling requests automatically.

## Official references used for implementation

- [OpenRouter current-key authentication](https://openrouter.ai/docs/api/api-reference/api-keys/get-current-api-key)
- [OpenRouter API errors and debugging](https://openrouter.ai/docs/api_reference/errors-and-debugging)
- [.NET Windows proxy snapshot and the .NET 9 refresh change](https://devblogs.microsoft.com/dotnet/dotnet-9-networking-improvements/)
- [.NET 8 system-proxy alias implementation](https://github.com/dotnet/runtime/blob/v8.0.21/src/libraries/System.Net.Requests/src/System/Net/WebRequest.cs#L494)
- [OpenAI model discovery](https://developers.openai.com/api/reference/resources/models/methods/list)
- [OpenAI Responses migration / structured output](https://developers.openai.com/api/docs/guides/migrate-to-responses)

- [Gemini Models API](https://ai.google.dev/api/models)
- [Gemini GenerateContent structured output](https://ai.google.dev/gemini-api/docs/generate-content/structured-output)
- [Anthropic model discovery/capabilities](https://platform.claude.com/docs/en/api/models/list)
- [Anthropic structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- [xAI language-model discovery](https://docs.x.ai/developers/rest-api-reference/inference/models)
- [xAI structured outputs](https://docs.x.ai/developers/model-capabilities/text/structured-outputs)

## Workspaces and pages

- **Generate** owns its draft, reference and conversation; creates new geometry. Facade and layer settings apply to concept output. Existing-object movement/scaling belongs in Assist.
- **Analyze** owns a separate conversation and scope. Local reports inspect real Rhino dimensions, geometry validity/closure and layer organization. AI interpretation is optional and read-only. It does not simulate structural, thermal, daylight or regulatory performance.
- **Model Assist** owns its draft/reference/conversation and selected/recent scope. Deterministic Move/Scale/Extrude controls create previews without an AI key. Distances and heights use document units; scaling uses a dimensionless factor around the scoped group's bounding-box center. Direction for curve extrusion is world +Z. AI requests can refine existing scoped targets.
- Only one viewport preview can be active. Switching workspace cancels that preview; drafts, references and conversations are retained separately for the current session. They are not persisted as project memory.
- **Connections** is a status overview. **API profiles**, **MCP connection**, **Preferences** and **About the creator** are separate pages. Opening the MCP page no longer starts the server automatically.
