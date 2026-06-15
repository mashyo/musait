// Copyright (c) 2026 Mashyo. All Rights Reserved.

namespace Musait.Models
{
    public enum MusaitEdition
    {
        Free
    }

    public static class MusaitCapabilities
    {
        public static readonly MusaitEdition CurrentEdition = MusaitEdition.Free;
        public static readonly bool CanCreateRfa = false;
    }
}
