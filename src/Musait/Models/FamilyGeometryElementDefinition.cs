// Copyright (c) 2026 Mashyo. All Rights Reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musait.Models
{
    public sealed class FamilyGeometryElementDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("kind")]
        public string Kind { get; set; } = "extrusion";

        [JsonProperty("solid_or_void")]
        public string SolidOrVoid { get; set; } = "solid";

        [JsonProperty("subcategory")]
        public string Subcategory { get; set; } = string.Empty;

        [JsonProperty("material")]
        public string Material { get; set; } = string.Empty;

        [JsonProperty("bounds")]
        public JObject Bounds { get; set; } = new();

        [JsonProperty("profile")]
        public JToken? Profile { get; set; }

        [JsonProperty("path")]
        public JToken? Path { get; set; }

        [JsonProperty("visibility")]
        public string Visibility { get; set; } = string.Empty;
    }
}
