# AI Archi-Tec Studio 0.6.5 for Windows

Run the Windows x64 MSI. It installs Studio for your Windows user, registers a Start menu shortcut, and includes the .NET runtime. Uninstall Studio in Windows Settings → Installed apps. The embedded website uses Microsoft Edge WebView2 Runtime.

Choose Sign in with browser. The default browser opens https://ai-arch.ir/desktop/sign-in. Sign in there with your existing website account, match the code from Studio and approve this device. Studio checks the approved device with the website backend. The device grant expires after 30 days and access is checked again for each session and operation. New users can request an account on the website. Open the account menu to see your account, workspace access, and Sign out.

Open Connect an app. Pick an installed app icon or choose an application and edition in Applications. Install bridge appears for supported Rhino, SketchUp, and Revit 2025 installations that need it. Open app launches the selected edition. Check connection reads the current document, or lists tools for a custom MCP server. The displayed time records the last successful check.

Click the AI Archi-Tec logo to open your private web workspace inside Studio. Sign in on the site. Studio keeps that web session in its own Windows user profile across updates. Choose Desktop chat to return to your draft. Open in browser launches the same platform in your default browser. The embedded panel uses the Microsoft Edge WebView2 Runtime.

Open Applications and choose a host from the collapsible sidebar, then choose its edition above the conversation. A configured local MCP profile can override an edition-specific route. Each target retains its own draft and reference image in this window. Use Configure connector to bind a trusted executable to that edition.

## Voice and reference images

Choose Reference to attach a PNG/JPEG. Review the thumbnail or remove it before sending. Record starts the microphone. Finish sends that clip to OpenAI transcription and inserts the transcript into the draft. Review and send the draft when ready. OpenRouter and OpenAI use the chat provider key for transcription. An optional OpenAI dictation key overrides that choice. The OpenAI chat key can also be used when OpenAI is selected.

## Conversation and outputs

Chat streams text when the provider supports streaming. Build uses document tools and shows their actual start, result and failure events under Activity. Stop ends further model steps; operations already dispatched can finish. Use Test model in Settings for a small authenticated request. OpenAI, Anthropic, Gemini, OpenRouter and compatible chat-completions endpoints are selectable. For a custom service enter the entire chat completions URL; HTTPS and local HTTP are supported. The selected model must support the features you request.

Save a response as Markdown, export a displayed table as CSV, or save a fenced text file or viewport image directly from the conversation. Workspace → Choose output folder sets the default destination for this session. AI Archi-Tec in the menu bar opens the platform, information about the studio and the contact email.

## Host connections

- Blender: open Connector files, then open StudioScene.py in Blender’s Text Editor and run it.
- 3ds Max: use Scripting → Run Script on StudioScene.py. The host must include pymxs and PySide2 or PySide6.
- SketchUp: in Window → Ruby Console use load with the absolute forward-slash path to architec_studio.rb. Reload after restarting the application.
- These three connectors use local session folders, run on the host UI thread, and accept native scene inspection and creation of indexed triangle meshes. Close the host to end its session.
- Archicad: open one matching project. Studio uses Graphisoft’s local JSON interface for element queries and supported property edits.
- Photoshop, Illustrator, InDesign and AutoCAD: open a document. The Windows automation route reads layers and creates a named layer. AutoCAD also accepts native WCS lines in model space. The application must expose its Windows automation object. Where registration is unavailable, configure an edition-specific MCP server.
- Grasshopper: enable the Rhino bridge. Inspect opens the Grasshopper context through the running Rhino session.
- Revit 2025 uses Studio’s local BIM connector. Other Revit editions require a matching MCP server. Edition choices describe routing targets, not certifications.


Open AI Archi-Tec Studio from the Windows Start menu.

## Choose a document

- Revit 2025: choose Install bridge in Studio, then launch Revit. Allow the local Archi-Tec add-in to load. Open one Revit process and activate the project to use. In Studio choose Revit and click Inspect. The response includes levels, selection and the session document ID.
- Rhino: install the AI Archi-Tec Rhino bridge, open a model, and enable its MCP connection on the port selected in Rhino. Studio discovers that port from the active Rhino session. Choose Rhino. Inspect reads the active document; Viewport captures its current image.
- Both: inspect each application. Ask to copy a straight Revit wall's location axis into Rhino. The transfer uses metres in internal/world coordinates. It copies a curve. It does not transfer a BIM wall, materials or shared survey coordinates.
- FreeCAD: choose Open FreeCAD from Studio to load the bundled macro. Open a document, then check the connection. Studio can inspect the selection and add a triangle mesh in millimeters.
- Maya: choose Open Maya from Studio to load the scene connector, then Check connection. Inspect selection and create indexed triangle meshes in centimetres.
- Add application: choose a trusted local executable implementing MCP over stdio, enter a unique application name and its command-line arguments as a JSON array. Inspect discovers the tools it offers. Host-specific setup and licensing still apply.

## Work in Studio

For ChatGPT without an API key, install the Codex CLI, use Sign in to Codex with ChatGPT or run codex login, and select ChatGPT via Codex in Studio. Each request uses the CLI in a temporary read-only workspace. Studio dispatches design operations only in Build mode. Windows + H can dictate into the draft without an OpenAI transcription key.

Open Settings, choose a provider, enter a supported model ID and your personal key. The key stays in this window's memory. Selected images, conversation and model tool results used in a turn are submitted to the chosen provider. Chat is conversation-only. Build enables the selected host's tools. Attach a PNG or JPEG for a vision-capable model, or attach one UTF-8 TXT, Markdown, CSV or JSON reference up to 96 KB. Send with Enter or Ctrl+Enter. Shift+Enter inserts a new line. New chat clears the current host's conversation. Switching hosts preserves separate conversations while the window is open.

In Revit try: “On level L1 create a straight 5 m wall, 3 m high.” Then: “Raise that same wall to 3.6 m and read back its height.” Check Undo inside Revit. Edits are not automatically saved to disk.

## Work in an external AI client

Select the desired host and choose Connect to Codex to register it as a local MCP server, then open a fresh Codex conversation. For manual setup select Orchestration → External MCP configuration. Studio displays the Claude Desktop JSON and a Codex CLI registration command. The client launches the same Studio executable with --mcp and the selected host. Host selection is fixed for that registration. The Studio window does not need to remain open. The design application and its connection must be running.

## Scope of this local build

Revit provides project inspection, wall inspection, straight wall creation and unconnected height editing. Rhino exposes the installed bridge's tool catalog. Generic hosts expose their own catalog. API provider eligibility, costs and image support depend on the selected provider and model. The app lists tool receipts and keeps no cloud project store.
Version 0.6.5 adds native Revit 2025 model lines and save for named local projects, and AutoCAD entity moves and DWG save. For 3ds Max and SketchUp request a native copy, OBJ export or PNG. SketchUp PNG captures the viewport. 3ds Max uses its active renderer. Output receipts contain the local path. Available commands are listed by tools/list. Update the bridge and reopen the host to load changed connectors.
