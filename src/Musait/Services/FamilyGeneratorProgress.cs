// Copyright (c) 2026 Mashyo. All Rights Reserved.

namespace Musait.Services
{
    public sealed class FamilyGeneratorProgress
    {
        public int CompletedSteps { get; set; }
        public int Percent { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
