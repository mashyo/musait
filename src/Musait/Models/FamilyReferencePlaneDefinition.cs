// Copyright (c) 2026 Mashyo. All Rights Reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musait.Models
{
    public sealed class FamilyReferencePlaneDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("direction")]
        public string Direction { get; set; } = "x";

        [JsonProperty("offset")]
        public JToken Offset { get; set; } = new JValue(0);

        [JsonProperty("is_reference")]
        public string IsReference { get; set; } = "Weak";
    }
}
