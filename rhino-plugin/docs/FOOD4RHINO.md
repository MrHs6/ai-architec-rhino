# Distribution draft

Product: **AI Archi-Tec for Rhino**
Owner: **Hasan Sharif Askari**
Website: https://ai-arch.ir
Version: **0.4.2-beta.1**
Platform: Rhino 8.31+, Windows x64, .NET 8. Tested with Rhino 8.31.

## Listing text

AI Archi-Tec turns architectural conversation into editable native Rhino geometry. Open the dockable panel with `archtec`. Generate new concept geometry, inspect a read-only model report, or refine selected objects and recent results in Model Assist. Review the viewport preview, Apply, then receive a measured result report in chat. Continue editing the same object GUIDs; manual changes and Undo refresh the recent-result feedback.

Bring your own OpenAI, OpenRouter, Gemini, Anthropic/Claude, xAI/Grok or compatible/local API key, or connect a compatible desktop AI client through authenticated localhost MCP. Profiles, model choices and key-storage preferences are independent. Save and network status are separate, and a no-key network test helps diagnose failed connections. Direct OpenAI uses the Responses API with structured output.

Connect Codex or Claude Desktop from the MCP page with automatic per-user registration. Chat in the AI client; Rhino shows replies, previews and Apply. The bridge recovers after Rhino MCP off/on without another client reload. Initial setup or a plugin upgrade may require one client refresh. Browser ChatGPT connectors/tunnels are not provisioned.

Concept-modeling beta for Rhino 8.31+ on Windows x64 (.NET 8). Includes rectangular multi-floor buildings with slabs, columns and facades, boxes, curves, extrusion, movement, scaling, layers and scoped property reports. Limited offline examples work without a key. User accounts and API billing stay with the chosen provider; no shared key is included. Optional remembered keys use Windows DPAPI for the current Windows user. Model history is session-based and separated by document.

Grasshopper graph authoring, photorealistic rendering, unrestricted image reconstruction, automatic code execution and engineering simulations are not included. Live provider/vision acceptance still depends on the user's account and selected model.

## متن کوتاه فارسی

آرشتک یک دستیار معماری داخل Rhino 8 است. با فرمان `archtec` پنل را باز کنید؛ مدل مفهومی بسازید، نتیجه را بررسی کنید و با گفت‌وگو همان مدل را تغییر دهید. هر اجرا پیش‌نمایش و Apply دارد؛ چت پس از اجرا از هندسهٔ واقعی گزارش می‌دهد و Undo در خود راینو کار می‌کند. API شخصی چند سرویس و MCP محلی، گزینه‌های مستقل اتصال هستند. کلید اختیاری به‌صورت رمزگذاری‌شده روی همان حساب ویندوز ذخیره می‌شود. نسخهٔ فعلی ویندوزی و آزمایشی است.

## Listing fields and attachments

- Product / command: AI Archi-Tec for Rhino / `archtec`.
- Author: Hasan Sharif Askari. Website: https://ai-arch.ir
- Project, installation and support: https://github.com/MrHs6/ai-architec-rhino
- Categories to choose from the current form: Architecture, Artificial Intelligence and Modeling, where available.
- Version: 0.4.2-beta.1; Rhino 8.31+; Windows x64; .NET 8.
- License: proprietary / source available for inspection under the repository's actual LICENSE.md. Do not label it MIT or open source.
- Attach the tested Yak or installer ZIP, SHA256SUMS, the original product logo, and genuine Generate/API/result screenshots from this build.
- Include the API cost note, current beta limitations, installation tutorial and change log. Screenshots must not expose credentials or MCP session tokens.

## Publication steps for owner

1. Review the built `.yak`, privacy text, screenshots and ownership/license terms. No license to redistribute source has been assumed; source is available in the dedicated public plugin repository.
2. Sign into the owner's McNeel/Yak account and upload the tested package with the Yak CLI, or use the owner's normal release process. Public package-name availability must be checked at publication time.
3. Create or update the Food4Rhino app page, link the Yak package, and add the listing text, genuine plugin screenshots, beta status, requirements and support information.
4. Test installation from the published listing on a separate licensed Windows/Rhino machine before marking the release stable.

This document is a ready-to-enter listing draft. It is not evidence of a published Food4Rhino or Package Manager listing. McNeel authentication and publication remain owner-account operations.

References: [Yak package structure](https://developer.rhino3d.com/en/guides/yak/the-anatomy-of-a-package/), [manifest](https://developer.rhino3d.com/en/guides/yak/the-package-manifest/), [Rhino plugin installation](https://www.rhino3d.com/en/docs/guides/scripts-plugins/how-to-use/).
