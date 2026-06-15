// Copyright (c) 2026 Mashyo. All Rights Reserved.

namespace Musait.Services
{
    public sealed class FamilyGeneratorResult
    {
        private FamilyGeneratorResult(bool succeeded, string outputRfaPath, string message)
        {
            Succeeded = succeeded;
            OutputRfaPath = outputRfaPath;
            Message = message;
        }

        public bool Succeeded { get; }
        public string OutputRfaPath { get; }
        public string Message { get; }

        public static FamilyGeneratorResult Success(string outputRfaPath, string message)
        {
            return new FamilyGeneratorResult(true, outputRfaPath, message);
        }

        public static FamilyGeneratorResult Failure(string message)
        {
            return new FamilyGeneratorResult(false, string.Empty, message);
        }
    }
}
