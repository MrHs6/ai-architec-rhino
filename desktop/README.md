# AI Archi-Tec Studio 0.7.0 for Windows x64

Run the MSI and open Studio from Start. Enter your OpenRouter API key in the welcome window. Studio verifies it with OpenRouter before opening the workspace. The key stays in memory until you close Studio. A website account is separate and optional.

In API settings choose a Chat model and a Build model, or type an exact OpenRouter model ID in either field. Refresh the list to discover new models. The Build list shows models advertising tool calling. Image references require a vision-capable model. OpenRouter charges the account associated with the key.

For a design change, open a document, select its application and edition, and use Check connection. Install the native connector and restart the host if prompted. Rhino 8/9 uses the Studio native package, SketchUp its Ruby extension and Revit 2025 its add-in. Host licenses are separate. A connected AI key enables chat even with no document open.

Use Chat to discuss a design. Switch to Build to inspect the active document, request an edit and review its receipt. Voice input transcribes a recording through OpenRouter into your draft. End the session from Studio account, where the installed version is displayed.

[Download the Windows x64 installer](https://github.com/MrHs6/ai-architec-rhino/releases/download/studio-v0.7.0/AI-Archi-Tec-Studio-0.7.0-x64.msi) · [Product page](https://ai-arch.ir/studio/)

SHA-256: `5796c04edad5faafde7338089a32456338f1be71d09fcbbb45287cff8aecc8a9`

## Build and verify

Build on Windows using the .NET 8 SDK. Run `desktop/Package.ps1` with WiX 5 and its UI extension, the Revit 2025 assemblies and Rhino 9 installed. The Rhino 8 connector uses the NuGet SDK. Run `desktop/Test-Studio.ps1` for simulated OpenRouter checks. Live model calls and host edits still need verification on a licensed system with OpenRouter access.
