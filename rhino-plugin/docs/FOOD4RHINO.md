# Distribution draft

Product: **AI Archi-Tec for Rhino**
Owner: **Hasan Sharif Askari**
Website: https://ai-arch.ir
Version: **0.2.0-beta.3**
Platform: Rhino 8.31+, Windows x64, .NET 8. Tested with Rhino 8.31.

## Listing text

AI Archi-Tec connects architectural conversation to editable native Rhino geometry. Bring your own OpenRouter or compatible API key, or connect an AI desktop client over localhost MCP. Review proposed actions in the viewport, Apply explicitly, and use Rhino Undo. Includes a dockable native chat panel, selected-model context, editable multi-floor concept buildings, basic geometry tools and a stdio MCP bridge.

Early beta: automatic full-building design, Grasshopper and public cloud MCP hosting are not included. A provider account and API credit are needed for online AI; offline box/move examples are included. The user supplies their own API key. Windows only.

## Publication steps for owner

1. Review the built `.yak`, privacy text, screenshots and ownership/license terms. No license to redistribute source has been assumed; source is available in the dedicated public plugin repository.
2. Sign into the owner's McNeel/Yak account and upload the tested package with the Yak CLI, or use the owner's normal release process. Public package-name availability must be checked at publication time.
3. Create or update the Food4Rhino app page, link the Yak package, and add the listing text, genuine plugin screenshots, beta status, requirements and support information.
4. Test installation from the published listing on a separate licensed Windows/Rhino machine before marking the release stable.

This task creates the release files and a listing draft. It does not create a McNeel account or publish a public listing.

References: [Yak package structure](https://developer.rhino3d.com/en/guides/yak/the-anatomy-of-a-package/), [manifest](https://developer.rhino3d.com/en/guides/yak/the-package-manifest/), [Rhino plugin installation](https://www.rhino3d.com/en/docs/guides/scripts-plugins/how-to-use/).

