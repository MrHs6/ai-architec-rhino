# Continuing an MCP modeling conversation

## Connect once per Rhino session

Open `archtec`, choose MCP and connect your desktop AI client. Keep Rhino running. You can hide the panel, switch tabs, use another application and return to the same conversation. Finishing a modeling request does not stop the listener. An idle conversation does not expire the initialized HTTP session.

The panel is registered as a Rhino **System** panel, rather than a panel tied to one document. Its visibility events only pause/resume the visible context refresh. Listener shutdown, pending-request cancellation and preview cleanup are no longer side effects of `UnLoad`.

## Read, inspect and refine the existing model

1. Read `get_context` for the active `document_id`, units, layers, selected objects and recent targets. After opening a different file, read it again; requests for the old active document are rejected.
2. Use `get_objects` with explicit `object_ids` or a `layer_prefix`. It returns native IDs, names, layers, bounds, validity, closure, surface area and volume where supported. The brief context intentionally does not dump the entire model.
3. For a large model, request a bounded `limit` and follow `next_offset` as the next request's `offset`. A null `next_offset` means the last page. Restart at zero if the model changes between pages. Hidden geometry requires `include_hidden: true`.
4. Inspect the viewport with `get_viewport_image` where useful. For Grasshopper, read its context and discover the actual component ports.
5. Send a validated `execute_plan` with a new request UUID for the next edit, referencing the existing GUIDs. Inspect the receipt and the same objects afterward. Do not replace an existing model just because the next message asks for a refinement.

The AI client's conversation and context window remain client-owned. The plug-in retains document targets and bounded operation receipts for the running Rhino session; it does not provide unlimited AI memory or run a second planner.

## Understand connection status and Stop

- **Connected:** a client has made recent authenticated contact. The client controls the conversation and tool authorization.
- **Session ready · idle:** a valid initialized session has not contacted Rhino recently. This is not a dropped connection or a reason to press Start again. Last contact alone cannot prove that a remote client is still open.
- **Waiting for your AI client:** the local listener is enabled, with no tracked active client session. Client shutdown normally removes its session; the listener stays available.
- **Stop MCP:** explicitly disables the listener and local discovery and cancels pending server work. Start it again to allow a configured client to reconnect. An uncertain modeling request is never replayed automatically.
- **Stop in the AI client:** cancels the active request through MCP cancellation. It does not discard completed geometry or silently disable the entire server. Completed batches remain available to Rhino Undo.

Closing Rhino ends the server. Starting a different Rhino process requires enabling its MCP connection once; this release does not introduce auto-start or a background Rhino process. The existing STDIO bridge reconnects to an enabled listener without requiring a reload for each request.

## Verification in 0.6.0-beta.10

- 199 core/protocol checks pass, including real loopback HTTP and STDIO reconnect/cancellation checks. A controlled clock advances eight hours and verifies that the original HTTP session remains usable without initialization or fabricated last-contact timestamps.
- 15 installed native checks pass: real panel hide/show, the same listener/discovery endpoint, four consecutive context/target inspections while hidden, idle UI status, native object pagination and an isolated edit preserving GUID and a closed 24 m³ solid.
- The native acceptance helper makes zero cloud requests and does not modify the user's model. Its geometry checks use a separate headless document. See [the sanitized record](../rhino-plugin/docs/acceptance/mcp-lifetime-beta10.json).

These checks target session continuity, not unlimited model quality, a second-computer trial or new paid-provider acceptance. The 0.6.0-beta.9 provider checks remain historical records.

The panel registration follows [McNeel's documented System panel lifetime](https://developer.rhino3d.com/en/guides/rhinocommon/tabbed-panels/).
