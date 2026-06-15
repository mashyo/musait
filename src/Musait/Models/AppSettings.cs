// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;

namespace Musait.Models
{
    public class AppSettings
    {
        public bool IsDarkTheme { get; set; } = true;
        
        // Capture resolution
        public int CaptureResolutionIndex { get; set; } = 0; // 0 = 1024x768, etc.
        public bool AutoArchiveCaptures { get; set; } = false;
        public bool ForceWhiteBackground { get; set; } = false;
        public bool ChangeDisplayMode { get; set; } = true;
        public int TargetDisplayStyleInt { get; set; } = 2; // Shaded, etc.
        public bool IncludeRevitContext { get; set; } = false;
        public string PromptPresetId { get; set; } = "as-captured";
        public bool AutoSendCaptures { get; set; } = false;
        public string DefaultPromptMode { get; set; } = "Auto";
        public string CustomPromptText { get; set; } = string.Empty;
        public string VisualizeCustomPromptText { get; set; } = string.Empty;
        public string TrendsCustomPromptText { get; set; } = string.Empty;
        public int RenderQualityIndex { get; set; } = 0;
        public bool ShowTrendsInComposer { get; set; } = true;
        public bool ShowTrendsInActionBar { get; set; } = true;
        public string BuildFunctionId { get; set; } = "family";
        public bool RequireFamilyPreviewBeforeGenerate { get; set; } = true;
    }
}

