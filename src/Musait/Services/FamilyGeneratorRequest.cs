// Copyright (c) 2026 Mashyo. All Rights Reserved.

using Musait.Models;

namespace Musait.Services
{
    public enum FamilyGeneratorRequestKind
    {
        GenerateFamily,
        OpenFamily
    }

    public sealed class FamilyGeneratorRequest
    {
        public FamilyGeneratorRequestKind Kind { get; set; } = FamilyGeneratorRequestKind.GenerateFamily;
        public string JsonPath { get; set; } = string.Empty;
        public string OutputRfaPath { get; set; } = string.Empty;
        public FamilyBuildPlan? BuildPlan { get; set; }
        public bool OpenInFamilyEditor { get; set; }
        public bool OverwriteExisting { get; set; }
    }
}
