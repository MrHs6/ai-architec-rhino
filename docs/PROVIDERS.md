# Provider protocols and independent profiles

Each service has its own endpoint, credential, remembered-key choice and selected model. Switching services preserves unsaved profile drafts in memory. **Save this profile** persists preferences and makes the selected provider active. Keys are persisted only when Remember is enabled, using Windows DPAPI; otherwise the key remains available in this Rhino session, including the native Options page, and is cleared at exit. Connect saves first, then fetches the catalog. A failed network request does not discard the saved profile. Settings files are replaced atomically. Saving does not send a modeling request.

| Service | Discovery | Generation | Authentication |
|---|---|---|---|
| OpenRouter | `/api/v1/models`, filter tools/structured_outputs | OpenAI Chat Completions + JSON schema | Bearer header |
| OpenAI | `/v1/models`, exclude known non-text modalities | Responses + `text.format` JSON schema, `store:false` | Bearer header |
| xAI / Grok | `/v1/language-models`, text output + image metadata | Chat Completions + JSON schema | Bearer header |
| Google Gemini | `/v1beta/models`, paginated; Gemini text generation | Native `models/<id>:generateContent`, structured generation config | `x-goog-api-key` header |
| Anthropic / Claude | `/v1/models`, paginated; capability metadata | Native `/v1/messages`, `output_config.format` | `x-api-key`, `anthropic-version` headers |
| OpenAI-compatible / local | `/models` on user-specified base | `/chat/completions` + JSON schema | Bearer header |

Gemini sends reference images as inline image parts. Anthropic sends base64 image content blocks; xAI and OpenRouter use image URL parts according to catalog image capabilities. Generic/OpenAI discovery does not confirm vision and image submission remains disabled there. Unsupported model/schema combinations fail explicitly; no silent provider fallback is attempted. Built-in Gemini, Anthropic and xAI credentials are restricted to their official API base URLs. Custom endpoints belong in the compatible/local profile.

The local transport tests use simulated official response shapes: credentials in headers, native payloads, model filtering/pagination, images, incomplete output and errors. **Live account-based acceptance still requires the user's own API keys.** These tests do not establish live provider availability or image interpretation quality.

## Official references used for implementation

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
