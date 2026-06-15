// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System.Collections.Generic;
using System.Linq;

namespace Musait.Services
{
    public enum PromptMode
    {
        Visualize,
        Trends,
        Build,
        None
    }

    public sealed class PromptPreset
    {
        public PromptPreset(
            string id,
            string name,
            string subtitle,
            PromptMode mode,
            string prompt,
            string accentStart = "#3A6F96",
            string accentEnd = "#88B8E0")
        {
            Id = id;
            Name = name;
            Subtitle = subtitle;
            Mode = mode;
            Prompt = prompt;
            AccentStart = accentStart;
            AccentEnd = accentEnd;
        }

        public string Id { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public PromptMode Mode { get; }
        public string Prompt { get; }
        public string AccentStart { get; }
        public string AccentEnd { get; }

        public string ModeGlyph => Mode switch
        {
            PromptMode.Visualize => "*",
            PromptMode.Trends => "T",
            PromptMode.Build => "B",
            _ => "-"
        };

        public string GroupName => Mode switch
        {
            PromptMode.Visualize => "VISUALIZE",
            PromptMode.Trends => "TRENDS",
            PromptMode.Build => "BUILD",
            _ => "OTHER"
        };

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class PromptModifier
    {
        public PromptModifier(string id, string label, string promptRule)
        {
            Id = id;
            Label = label;
            PromptRule = promptRule;
        }

        public string Id { get; }
        public string Label { get; }
        public string PromptRule { get; }
    }

    public static class PromptPresetCatalog
    {
        private const string FamilyJsonPrompt = """
You are a Revit Family JSON assistant for a reviewed local preview workflow.

If an image is uploaded: identify the object, infer visible proportions, then ask only what the image does not answer, usually category/host, approximate overall size, and detail level. If text only: ask exactly 3 focused questions before outputting JSON: intended Revit category and host type, approximate overall width/depth/height, and desired detail level.

After the user answers, return exactly one JSON object and nothing else. No markdown, no code fence, no comments, no prose.

The JSON must use schema "musait.family.rfa.v2" and this root shape:
{
  "schema": "musait.family.rfa.v2",
  "name": "Family Name",
  "category": "Furniture",
  "host": "non-hosted",
  "units": "mm",
  "capability": "static",
  "archetype": "",
  "reference_planes": [],
  "parameters": [],
  "geometry": [],
  "constraints": [],
  "materials": [],
  "subcategories": [],
  "features": {},
  "diagnostics": [],
  "_todo": []
}

Use host non-hosted, wall-hosted, floor-hosted, ceiling-hosted, or face-based. Use units mm, cm, m, in, or ft. Prefer mm for furniture and casework unless the user requests another unit.

Set capability honestly:
- static: reliable geometry, no native flex claim
- hybrid: selected Revit-native controls are intended, but not full flex
- native_parametric: only safe archetypes/patterns that can be built around reference planes and validated by Revit

Use archetype when a safe recipe applies:
- casework.wardrobe
- casework.cabinet
- casework.shelving
- furniture.table_basic

Reference planes define the family skeleton. Each reference plane object has:
{
  "name": "Right",
  "direction": "x",
  "offset": "Width",
  "is_reference": "Strong"
}

Use standard planes when useful: Left, Right, Front, Back, Bottom, Top, Center Left/Right, Center Front/Back. Direction is x, y, or z. Offset is a number or expression. Expressions reference parameter/plane names with spaces replaced by underscores.

Every parameter object has:
{
  "name": "Width",
  "type": "length",
  "instance_or_type": "type",
  "default_value": 1800,
  "group": "Dimensions"
}

Parameter type is length, angle, number, integer, yes_no, text, or material. Group is Dimensions, Materials and Finishes, Visibility, Constraints, Identity Data, or Data.

Geometry describes solids or voids. Prefer axis-aligned rectangular extrusions with bounds:
{
  "id": "top_panel",
  "kind": "extrusion",
  "solid_or_void": "solid",
  "subcategory": "Carcass",
  "material": "Body Material",
  "bounds": {
    "x0": "Left",
    "x1": "Right",
    "y0": "Front",
    "y1": "Back",
    "z0": "Top - Panel_Thickness",
    "z1": "Top"
  }
}

Initial geometry kind values are extrusion, sweep, revolution, and void_extrusion. For static/freeform-looking objects, approximate with clean bounded extrusions and put unsupported curvature or decorative details in _todo.

Constraints may use only align, dimension, formula, visibility, or material. For static output, constraints can be empty. For hybrid/native output, include only relationships that map to Revit-native behavior:
{"type":"dimension","name":"Overall Width","from":"Left","to":"Right","parameter":"Width"}
{"type":"align","target":"top_panel.z1","reference_plane":"Top","locked":true}
{"type":"material","target":"top_panel","parameter":"Body Material"}

Materials is an array, for example:
[{"name":"Body Material","appearance":"painted MDF"}]

Subcategories is an array of strings, for example:
["Carcass","Doors","Hardware"]

Features is an object. Diagnostics is an array of objects with severity and message. _todo is an array of strings.

For casework, wardrobes, cabinets, and shelving, use category Casework when appropriate. Include Width, Depth, Height, Panel Thickness, and material parameters. Build major panels from reference-plane bounds.

For simple tables, use archetype furniture.table_basic when possible. Use Width, Depth, Height, Top Thickness, Leg Thickness, Leg Inset, Body Material. Use clean reference planes and bounded extrusions.

Before final output, self-check: exact v2 root shape, valid host/units/capability, reference planes use direction/offset, parameters use default_value/instance_or_type/group, geometry uses kind/solid_or_void/bounds, constraints use supported types, materials/subcategories are arrays, features is an object, and every bound expression can be evaluated from parameters or reference planes.

Use appended BIM context as secondary reference only.
""";

        public static IReadOnlyList<PromptPreset> Presets { get; } = new List<PromptPreset>
        {
            new PromptPreset(
                "as-captured",
                "As Captured",
                "style only",
                PromptMode.Visualize,
                "",
                "#30343A",
                "#68707A"),
            new PromptPreset(
                "exterior-render",
                "Exterior render",
                "golden hour",
                PromptMode.Visualize,
                "Transform this Revit capture into a photorealistic architectural exterior visualization. Preserve the building's exact geometry, massing, and proportions. Photograph it as if with a 24 mm wide-angle lens during golden hour — warm low sun at about 30 degrees, long shadows, warm atmospheric haze on the horizon. Interpret and enhance the materials visible in the geometry; do not override them with unrelated materials. Set the building in a calm, well-kept landscape with mature trees and open ground cover. No people, no vehicles, no overlaid text. Output: 8 K editorial photography quality.",
                "#D4881E",
                "#F0C060"),
            new PromptPreset(
                "interior-atmosphere",
                "Interior atmosphere",
                "north daylight",
                PromptMode.Visualize,
                "Transform this Revit capture into a photorealistic interior photograph. Preserve the exact floor layout, ceiling height, room proportions, and all openings as shown. Photograph the space at standing eye level with a 35 mm lens. The room is lit by diffused daylight entering through the windows visible in the model, supplemented by warm 2700 K ambient light that creates a calm, inhabited atmosphere. Interpret the materials from what the model's geometry suggests — keep them refined and internally consistent. No people, no furniture clutter. Output: 8 K architectural photography quality.",
                "#4A82C0",
                "#88B8E0"),
            new PromptPreset(
                "material-study",
                "Material study",
                "surface study",
                PromptMode.Visualize,
                "Generate a close-up material study from this Revit capture. Identify the most prominent surface — primary facade cladding, structural wall, or floor plane — and examine it at roughly 1:5 to 1:10 scale. Reveal the material's texture, reflectance, grain, and physical logic. Render it as new construction: clean joints, precise detailing, no weathering or wear. Light it with soft, neutral overcast daylight that eliminates harsh shadow and renders color and finish accurately, as on a professional material sample board. Output: 8 K macro photography quality.",
                "#7A8490",
                "#AAB4BC"),
            new PromptPreset(
                "night-scene",
                "Night scene",
                "light spill",
                PromptMode.Visualize,
                "Transform this Revit capture into a dramatic night-time exterior visualization. Preserve the building's exact geometry and proportions. The sky is deep blue-black. Warm amber light spills from the interior through glazing, making the windows glow against the darkness. Subtle uplighting grazes the facade. The ground — whether paved, gravel, or lawn — catches and reflects the artificial light sources nearby. Photograph this with a 24 mm lens in a long-exposure cinematic mode: controlled, compressed, moody. No figures, no vehicles. Output: 8 K cinematic quality.",
                "#2A2E6A",
                "#4A4E9A"),
            new PromptPreset(
                "aerial-site",
                "Aerial site",
                "bird's-eye",
                PromptMode.Visualize,
                "Generate a bird's-eye perspective visualization of this building from approximately 60 m elevation, looking down at a 45-degree angle with natural perspective foreshortening. Preserve the building's exact massing, footprint, and roof composition from the model. Midday sun casts sharp, defined shadows that clearly reveal the massing logic and roof geometry. Surround the building with simplified site context: reduced-detail roads, paths, green space, and low-detail neighbouring structures. Clean, editorial image — high clarity, no atmospheric haze, sharp detail throughout. Output: 8 K editorial site visualization quality.",
                "#3A8E98",
                "#68B8C0"),
            new PromptPreset(
                "sketch-render",
                "Sketch",
                "line study",
                PromptMode.Visualize,
                "Transform this Revit capture into a precise architectural sketch. Preserve the exact geometry, camera angle, massing, openings, and proportions. Use confident graphite and ink linework with subtle construction lines, light paper texture, and restrained warm shadow washes. Keep the result legible as an architectural design drawing, not a loose fantasy illustration. No people, no vehicles, no overlaid text. Output: high-resolution presentation sketch quality.",
                "#5C4A38",
                "#907060"),
            new PromptPreset(
                "marker-render",
                "Marker",
                "presentation",
                PromptMode.Visualize,
                "Transform this Revit capture into a polished architectural marker rendering. Preserve the exact geometry, camera angle, massing, openings, and proportions. Use layered alcohol-marker washes, crisp ink outlines, soft paper grain, and controlled highlights to communicate material and depth. Keep the image disciplined and presentation-ready. No people, no vehicles, no overlaid text. Output: high-resolution hand-rendered presentation quality.",
                "#704818",
                "#A87030"),
            new PromptPreset(
                "custom-prompt",
                "Custom",
                "type directly",
                PromptMode.Visualize,
                string.Empty,
                "#4CAF82",
                "#C89A3C"),
            new PromptPreset(
                "glyph",
                "Glyph",
                "ink manifesto",
                PromptMode.Trends,
                "Transform this Revit capture into a post-digital architectural manifesto drawing. Near-monochrome ink field, single vivid color accent mark. Drawing-diagram hybrid: Mies van der Rohe pencil-elevation precision fused with Superstudio and Archigram conceptual boldness. Architecture drawn, not rendered — the image communicates concept as much as form. Collage texture on paper ground. Output: large-format manifesto print quality.",
                "#0D0D0D",
                "#8A78D8"),
            new PromptPreset(
                "signal",
                "Signal",
                "graphite diagram",
                PromptMode.Trends,
                "Render this Revit capture as a non-photorealistic representation hovering between diagram and image: axonometric precision fused with perspectival atmosphere. Cool graphite on white ground — shadow masses are the primary graphic instrument, not texture or color. Architecture understood simultaneously as built form and conceptual sign. Technical drawing clarity with poetic restraint. Preserve geometry. Output: print quality diagram-render.",
                "#1C2A38",
                "#8AAAC0"),
            new PromptPreset(
                "vellum",
                "Vellum",
                "pastel collage",
                PromptMode.Trends,
                "Render this Revit capture as a speculative intertextual collage. Soft pastel palette dominant — powder pinks, warm creams, pale blues on textured paper stock. Simultaneously austere and playful: architectural precision in composition, loose and painterly in mark-making. The drawing carries independent artistic authority, a meditation on the building as much as a depiction of it. Preserve geometry. Output: fine art architectural print quality.",
                "#F0C8C0",
                "#B8D0F0"),
            new PromptPreset(
                "fauve",
                "Fauve",
                "vivid primaries",
                PromptMode.Trends,
                "Transform this Revit capture into a naïve-art architectural dreamscape. Fauvist palette: vivid oranges, acid greens, saturated yellows and primaries. Flat color fields with minimal shadow gradient. Spatial logic drawn from naïve painting — Henri Rousseau jungles and Marc Chagall dreamscapes — festive and inhabited by memory rather than physics. Folk-painting directness and intensity. Preserve geometry. Output: editorial art quality.",
                "#F97316",
                "#4ADE80"),
            new PromptPreset(
                "mirage",
                "Mirage",
                "warm stillness",
                PromptMode.Trends,
                "Render this Revit capture as a dreamlike metaphysical composition. Warm pastel architecture — soft terracottas, muted ochres, pale creams — under sharp Mediterranean light casting precise geometric shadows. Clean abstract spaces with surrealist stillness: empty courtyards, unoccupied arches, no figures. De Chirico-like suspended timelessness — warm, quiet, out of time. Minimum detail, maximum atmosphere. Preserve geometry. Output: fine art print quality.",
                "#DBA882",
                "#88C0D4"),
            new PromptPreset(
                "family",
                "Family",
                "image to JSON",
                PromptMode.Build,
                FamilyJsonPrompt),
            new PromptPreset("none", "No prompt", "type in Gemini", PromptMode.None, string.Empty, "#666666", "#8A8A8A")
        };

        public static IReadOnlyList<PromptModifier> Modifiers { get; } = new List<PromptModifier>
        {
            new PromptModifier(
                "preserve-geometry",
                "geometry",
                "Preserve the visible building geometry, massing, openings, and proportions exactly."),
            new PromptModifier(
                "preserve-camera",
                "camera",
                "Keep the same camera viewpoint, lens feel, perspective, and framing."),
            new PromptModifier(
                "preserve-materials",
                "materials",
                "Respect visible model materials and only enhance them realistically."),
            new PromptModifier(
                "preserve-ratio",
                "ratio",
                "Keep the output composition aligned to the source image aspect ratio."),
            new PromptModifier(
                "avoid-invented-elements",
                "invented elements",
                "Do not add new building volumes, extra floors, facade openings, balconies, stairs, roofs, or structural elements."),
            new PromptModifier(
                "avoid-text",
                "text",
                "Do not add text, labels, signage, logos, watermarks, or fake annotations."),
            new PromptModifier(
                "avoid-people",
                "people",
                "Do not add people, silhouettes, crowds, mannequins, or figures."),
            new PromptModifier(
                "avoid-vehicles",
                "vehicles",
                "Do not add cars, trucks, bikes, buses, or other vehicles.")
        };

        public static IReadOnlyList<PromptPreset> GetActionBarPresets(bool showTrends)
        {
            return Presets.Where(p => showTrends || p.Mode != PromptMode.Trends).ToList();
        }

        public static IReadOnlyList<PromptPreset> GetByMode(PromptMode mode)
        {
            return Presets.Where(p => p.Mode == mode).ToList();
        }

        public static PromptPreset GetById(string id)
        {
            return Presets.FirstOrDefault(p => p.Id == id) ?? Presets.First(p => p.Id == "as-captured");
        }

        public static PromptPreset GetDefaultForMode(PromptMode mode)
        {
            return Presets.FirstOrDefault(p => p.Mode == mode) ?? GetById("exterior-render");
        }

        public static string BuildPrompt(string presetId, string revitContext, bool includeContext)
        {
            return BuildPrompt(presetId, revitContext, includeContext, Enumerable.Empty<string>());
        }

        public static string BuildPrompt(string presetId, string revitContext, bool includeContext, IEnumerable<string> modifierIds)
        {
            var preset = GetById(presetId);
            if (preset.Id == "none" && (!includeContext || string.IsNullOrWhiteSpace(revitContext)))
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(preset.Prompt))
            {
                parts.Add(preset.Prompt);
            }

            if (includeContext && !string.IsNullOrWhiteSpace(revitContext))
            {
                parts.Add(revitContext.Trim());
            }

            var modifierRules = Modifiers
                .Where(modifier => modifierIds.Contains(modifier.Id))
                .Select(modifier => "- " + modifier.PromptRule)
                .ToList();

            if ((preset.Mode == PromptMode.Visualize || preset.Mode == PromptMode.Trends) && modifierRules.Count > 0)
            {
                parts.Add("Additional prompt options:\n" + string.Join("\n", modifierRules));
            }

            if (parts.Count > 0)
            {
                parts.Add(preset.Mode == PromptMode.Build
                    ? "Produce a controlled artifact or educational answer. Do not validate construction, code, structural, MEP, cost, or life-safety decisions."
                    : "Use the image as the primary source. Preserve visible geometry and proportions unless the prompt explicitly asks for representational reinterpretation.");
            }

            return string.Join("\n\n", parts);
        }
    }
}
