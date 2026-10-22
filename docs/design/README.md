# Documentation visual system

The product cover and diagrams are original editable vector assets for this repository. All labels are English. They use a shared dark background, violet action path, mint native-result accent and Segoe UI/Arial typography. They are technical illustrations, not screenshots or generated-model quality claims.

| Asset | Usage |
|---|---|
| `product-cover.svg` / `.png` | Repository introduction and shareable product graphic |
| `modeling-workflow.svg` / `.png` | Describe → Validate → Preview → Apply → Refine |
| `connection-architecture.svg` / `.png` | API and desktop MCP routes, Rhino execution and per-user storage |
| `installation-quickstart.svg` / `.png` | Download, install, open Studio, then choose a conversation route |

The editable sources and PNG exports are in [docs/assets](../assets). Generate SVGs using Python's standard library:

```text
python docs/design/generate_assets.py
```

Export each SVG to PNG at its original pixel size using an SVG renderer, then visually inspect it before committing. GitHub uses the committed PNGs for consistent rendering; the SVGs remain editable and include accessible titles/descriptions. Keep the actual logo unchanged in its existing asset file.

Product screenshots in `rhino-plugin/docs` are real native UI captures. The five-floor building is a limited offline example from an earlier interface; the Codex box is connection acceptance evidence. Neither is evidence of unrestricted AI reconstruction, rendering, BIM or engineering analysis.

Download the graphics individually from their GitHub file pages or use the visual-kit ZIP attached to the 0.4.2 beta release. Retain the illustration/demo labels and the actual license terms when presenting the product.
