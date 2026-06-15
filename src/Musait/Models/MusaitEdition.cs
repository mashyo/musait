// Copyright (c) 2026 Mashyo. All Rights Reserved.

namespace Musait.Models
{
    public enum MusaitEdition
    {
        Free
    }

    public static class MusaitCapabilities
    {
        public const MusaitEdition CurrentEdition = MusaitEdition.Free;
        public const bool CanCreateRfa = false;
    }
}
