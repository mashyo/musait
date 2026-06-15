// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Musait.Models
{
    public sealed class FamilyDefinition
    {
        [JsonProperty("category")]
        public string Category { get; set; } = string.Empty;

        [JsonProperty("host")]
        public string Host { get; set; } = "non-hosted";

        [JsonProperty("units")]
        public string Units { get; set; } = "mm";

        [JsonProperty("schema")]
        public string Schema { get; set; } = "musait.family.v1";

        [JsonProperty("capability")]
        public string Capability { get; set; } = "static";

        [JsonProperty("archetype")]
        public string Archetype { get; set; } = string.Empty;

        [JsonProperty("components")]
        public List<FamilyComponentDefinition> Components { get; set; } = new();

        [JsonProperty("parameters")]
        public List<FamilyParameterDefinition> Parameters { get; set; } = new();

        [JsonProperty("bindings")]
        public List<FamilyParameterBindingDefinition> Bindings { get; set; } = new();

        [JsonProperty("repeaters")]
        public List<FamilyRepeaterDefinition> Repeaters { get; set; } = new();

        [JsonProperty("diagnostics")]
        public List<FamilyRigDiagnostic> Diagnostics { get; set; } = new();
    }

    public sealed class FamilyComponentDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("geometry")]
        public string Geometry { get; set; } = "extrusion";

        [JsonProperty("dims")]
        public FamilyComponentDimensions Dimensions { get; set; } = new();

        [JsonProperty("material")]
        public string Material { get; set; } = string.Empty;

        [JsonProperty("finish")]
        public string Finish { get; set; } = string.Empty;

        [JsonProperty("radius")]
        public double? Radius { get; set; }

        [JsonProperty("origin")]
        public FamilyPointDefinition Origin { get; set; } = new();

        [JsonProperty("rotation")]
        public FamilyRotationDefinition Rotation { get; set; } = new();

        [JsonProperty("role")]
        public string Role { get; set; } = "component";

        [JsonProperty("isVoid")]
        public bool IsVoid { get; set; }

        [JsonProperty("isVisible")]
        public bool IsVisible { get; set; } = true;
    }

    public sealed class FamilyComponentDimensions
    {
        [JsonProperty("w")]
        public double Width { get; set; }

        [JsonProperty("d")]
        public double Depth { get; set; }

        [JsonProperty("h")]
        public double Height { get; set; }
    }

    public sealed class FamilyPointDefinition
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public sealed class FamilyRotationDefinition
    {
        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public sealed class FamilyParameterDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = "Length";

        [JsonProperty("default")]
        public object? Default { get; set; }

        [JsonProperty("instance")]
        public bool Instance { get; set; }
    }

    public sealed class FamilyParameterBindingDefinition
    {
        [JsonProperty("parameter")]
        public string Parameter { get; set; } = string.Empty;

        [JsonProperty("targets")]
        public List<FamilyParameterBindingTargetDefinition> Targets { get; set; } = new();

        [JsonProperty("inferred")]
        public bool Inferred { get; set; }
    }

    public sealed class FamilyParameterBindingTargetDefinition
    {
        [JsonProperty("component")]
        public string Component { get; set; } = string.Empty;

        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("expression")]
        public string Expression { get; set; } = string.Empty;
    }

    public sealed class FamilyRepeaterDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("templateComponent")]
        public string TemplateComponent { get; set; } = string.Empty;

        [JsonProperty("countParameter")]
        public string CountParameter { get; set; } = string.Empty;

        [JsonProperty("axis")]
        public string Axis { get; set; } = "z";

        [JsonProperty("start")]
        public string Start { get; set; } = string.Empty;

        [JsonProperty("spacing")]
        public string Spacing { get; set; } = string.Empty;
    }

    public sealed class FamilyDefinitionValidationResult
    {
        public FamilyDefinitionValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
