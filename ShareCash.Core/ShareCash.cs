using System;
using System.IO.MemoryMappedFiles;

namespace ShareCash.Core
{
    public class ShareCash
    {
        public const int UserInteractionCheckInterval = 5 * 1000;
        public const int UserInteractionValue = 1;
        public const string PipeName = "ShareCashUserInteraction";
        public const string UserInteractionSignalPath = @"c:\sharecash\UserInteractionSignaler.txt";

        public static MemoryMappedFile GetUserInteractionNotifier() => MemoryMappedFile.CreateOrOpen("ShareCash", 1, MemoryMappedFileAccess.ReadWrite);
    }
}
