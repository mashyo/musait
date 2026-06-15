// Copyright (c) 2026 Mashyo. All Rights Reserved.

using Newtonsoft.Json;

namespace Musait.Models
{
    public sealed class FamilyConstraintDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("target")]
        public string Target { get; set; } = string.Empty;

        [JsonProperty("reference_plane")]
        public string ReferencePlane { get; set; } = string.Empty;

        [JsonProperty("locked")]
        public bool Locked { get; set; } = true;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("from")]
        public string From { get; set; } = string.Empty;

        [JsonProperty("to")]
        public string To { get; set; } = string.Empty;

        [JsonProperty("parameter")]
        public string Parameter { get; set; } = string.Empty;

        [JsonProperty("expression")]
        public string Expression { get; set; } = string.Empty;
    }
}
