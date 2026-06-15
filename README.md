# Musait Free - Renderings and Family Preview for Revit

**Musait Free connects Autodesk Revit to Google Gemini and AI Studio for architectural visualization, Trends exploration, and a transparent Family JSON preview workflow.**

[![License: Musait Source-Available License](https://img.shields.io/badge/License-Musait_Source--Available-blue.svg)](LICENSE)
[![Revit 2022-2027](https://img.shields.io/badge/Revit-2022--2027-orange.svg)](https://www.autodesk.com/products/revit)

Musait Free is source-available for inspection, local builds, and trust. It is not open source unless the license changes to an OSI-compatible license.

## Core Workflows

### Visualize
Generate high-fidelity architectural visualizations from 2D or 3D Revit views.

### Trends
Explore conceptual design directions through non-photorealistic artistic styles.
* Optional lean Revit context for view name, type, scale, units, and selected elements.

### Family Preview
Use AI Studio to create strict Family JSON from captures, reference images, or pasted JSON. Musait Free can normalize, validate, save, copy, and preview that JSON locally in the 3D previewer.

Musait Pro converts valid previews into `.rfa` files with Revit parameters, materials, geometry, and save/open handling. The Free repository does not contain the RFA creation engine.

## Installation

1. Download **`Musait-Setup.exe`** from the [latest GitHub Release](https://github.com/mashyo/musait/releases).
2. Run the installer. It will detect and configure the plugin for Revit versions 2022 through 2027.
3. Launch Revit and locate the **Mashyo Tools** tab on the ribbon.
4. Open the panel from the **Gemini Rendering** group and sign in to Gemini or AI Studio.

GitHub Releases are the canonical download route for Musait Free.

## Technical Specifications

* **Compatibility:** Revit 2022, 2023, 2024, 2025, 2026, and 2027.
* **Requirement:** Microsoft WebView2 Runtime.
* **Architecture:** C#/.NET WPF dockable pane with Revit `ExternalEvent` usage for supported Revit API actions.

## Public Boundary

This repository may include Family JSON models, validators, normalizers, build-plan creation for preview, prompt catalog entries, and the WebView2 3D previewer.

This repository must not include code that creates, saves, opens, exports, downloads, or stages `.rfa` output.

## Contributing

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions and pull request guidelines.

## License and Support

Musait Free is licensed under the [Musait Source-Available License](LICENSE). Pro support and RFA conversion are available through:

* [Patreon](https://patreon.com/mashyo)
* [GitHub Sponsors](https://github.com/sponsors/mashyo)
