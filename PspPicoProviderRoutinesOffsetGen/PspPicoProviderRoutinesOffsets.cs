namespace PspPicoProviderRoutinesOffsetGen;

public class PspPicoProviderRoutinesOffsets
{
    public struct Values
    {
        public ulong PspPicoRegistrationDisabled { get; set; }
        public ulong PspPicoProviderRoutines { get; set; }
        public ulong PspCreatePicoProcess { get; set; }
        public ulong PspCreatePicoThread { get; set; }
        public ulong PspGetPicoProcessContext { get; set; }
        public ulong PspGetPicoThreadContext { get; set; }
        public ulong PspPicoGetContextThreadEx { get; set; }
        public ulong PspPicoSetContextThreadEx { get; set; }
        public ulong PspTerminateThreadByPointer { get; set; }
        public ulong PsResumeThread { get; set; }
        public ulong PspSetPicoThreadDescriptorBase { get; set; }
        public ulong PsSuspendThread { get; set; }
        public ulong PspTerminatePicoProcess { get; set; }
    }

    public string Version { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public Values Offsets { get; set; } = new();
}
