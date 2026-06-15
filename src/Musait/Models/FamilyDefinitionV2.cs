// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musait.Models
{
    public sealed class FamilyDefinitionV2
    {
        public const string SchemaId = "musait.family.rfa.v2";

        [JsonProperty("schema")]
        public string Schema { get; set; } = SchemaId;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("category")]
        public string Category { get; set; } = string.Empty;

        [JsonProperty("host")]
        public string Host { get; set; } = "non-hosted";

        [JsonProperty("units")]
        public string Units { get; set; } = "mm";

        [JsonProperty("capability")]
        public string Capability { get; set; } = "static";

        [JsonProperty("archetype")]
        public string Archetype { get; set; } = string.Empty;

        [JsonProperty("reference_planes")]
        public List<FamilyReferencePlaneDefinition> ReferencePlanes { get; set; } = new();

        [JsonProperty("parameters")]
        public List<FamilyParameterDefinitionV2> Parameters { get; set; } = new();

        [JsonProperty("geometry")]
        public List<FamilyGeometryElementDefinition> Geometry { get; set; } = new();

        [JsonProperty("constraints")]
        public List<FamilyConstraintDefinition> Constraints { get; set; } = new();

        [JsonProperty("materials")]
        public List<FamilyMaterialDefinitionV2> Materials { get; set; } = new();

        [JsonProperty("subcategories")]
        public List<string> Subcategories { get; set; } = new();

        [JsonProperty("features")]
        public JObject Features { get; set; } = new();

        [JsonProperty("diagnostics")]
        public List<FamilyRigDiagnostic> Diagnostics { get; set; } = new();

        [JsonProperty("_todo")]
        public List<string> Todo { get; set; } = new();
    }

    public sealed class FamilyParameterDefinitionV2
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = "length";

        [JsonProperty("instance_or_type")]
        public string InstanceOrType { get; set; } = "type";

        [JsonProperty("default_value")]
        public object? DefaultValue { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; } = "Data";

        [JsonProperty("formula")]
        public string Formula { get; set; } = string.Empty;

        [JsonProperty("visible_to_user")]
        public bool VisibleToUser { get; set; } = true;
    }

    public sealed class FamilyMaterialDefinitionV2
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("appearance")]
        public string Appearance { get; set; } = string.Empty;
    }
}
