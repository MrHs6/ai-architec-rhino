# AI Archi-Tec Studio for Windows

Connect an architectural design document to a conversation. Inspect the active selection, propose an edit in Build mode, and read the host’s receipt before continuing.

[Download the Windows x64 installer](https://github.com/MrHs6/ai-architec-rhino/releases/download/studio-v0.6.5/AI-Archi-Tec-Studio-0.6.5-x64.msi) · [Installation and host guide](Quickstart.md) · [Product page](https://ai-arch.ir/studio/)

## Install and connect

1. Close Studio and run the MSI on Windows 10 or 11 x64. It installs for the current user and includes .NET. The installer is unsigned. SHA-256: **1D3F1C55609E8BA0CE08904B17A1801A9DCF0CF76BA6E40994238AEE873574A1**.
2. Launch Studio from Start, choose **Sign in with browser**, and approve the matching device code on ai-arch.ir. Access requires an authorized website workspace. New users can request access at [AI Archi-Tec](https://ai-arch.ir/home).
3. Open a licensed host and a test document. In **Applications**, select its edition, install the bridge if offered, then **Check connection**. Enable MCP in the Rhino panel. For SketchUp, restart after installing its Ruby bridge.
4. In **API settings**, enter your own provider key and model, or select **ChatGPT via Codex** with a signed-in Codex CLI. Test the connection. Chat discusses the design; Build can change the active document. Review activity and tool receipts.
5. To use an external client, select **Connect to Codex** or copy the MCP configuration into Claude Desktop or Gemini CLI. Keep the host open.

## Build from source

Build desktop/Studio.csproj with the .NET 8 SDK on Windows. For the MSI use desktop/Package.ps1 with WiX and its UI extension, the Revit 2025 SDK assemblies and the two Rhino 0.6.1-beta.1 Yak packages (pass their directory through -RhinoPackages). The acceptance project is in desktop-tests; run it with the argument --connections for simulated host and provider tests.

## Evaluation scope

The 0.6.5 build passed MCP handshake and catalog checks for Rhino, SketchUp, 3ds Max, Revit and AutoCAD, plus simulated multi-turn API and model-editing tests. Host licenses, supported exporters, AI usage and provider accounts are supplied separately. Live rendering and editing across all host versions have not been certified.
