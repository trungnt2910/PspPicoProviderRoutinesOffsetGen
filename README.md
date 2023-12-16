# PspPicoProviderRoutinesOffsetGen

[![Discord Invite](https://dcbadge.vercel.app/api/server/bcV3gXGtsJ?style=flat)](https://discord.gg/bcV3gXGtsJ)&nbsp;
[![Generate Offsets](https://github.com/trungnt2910/PspPicoProviderRoutinesOffsetGen/actions/workflows/generate.yml/badge.svg)](https://github.com/trungnt2910/PspPicoProviderRoutinesOffsetGen/actions/workflows/generate.yml)

Offsets from `ntosknrl.exe` that are useful for Pico providers.

## Included offsets

```
PspPicoRegistrationDisabled
PspPicoProviderRoutines
PspCreatePicoProcess
PspCreatePicoThread
PspGetPicoProcessContext
PspGetPicoThreadContext
PspPicoGetContextThreadEx
PspPicoSetContextThreadEx
PspTerminateThreadByPointer
PsResumeThread
PspSetPicoThreadDescriptorBase
PsSuspendThread
PspTerminatePicoProcess
```

## Obtaining offsets

You can run this thing on any Windows 10 machine. Other .NET platforms are NOT supported as native
`dbghelp.dll` functions are used for reading PDBs. If you have other alternatives to parse PDB
files, preferably from memory, feel free to open an issue or a PR.

I recommend you NOT to run this on your computer and waste gigabytes of bandwidth. Instead, check
out the resulting files on the
[`dist`](https://github.com/trungnt2910/PspPicoProviderRoutinesOffsetGen/tree/dist) branch in
both `.json` data and `.cpp` declaration format. Alternatively, check out
[`lxmonika`](https://github.com/trungnt2910/lxmonika/tree/master/lxmonika) if you are trying to
build your own Pico providers.

If you really need to run this code yourself, it is better to force Microsoft to pay for their own
sins. Fork this repo and dispatch a `Generate Offsets` workflow or run it in a Codespace.

## Community

Need help using this project? Join me on [Discord](https://discord.gg/bcV3gXGtsJ) and find a
solution together.

## Acknowledgements

- [**@rajkumar-rangaraj**](https://github.com/rajkumar-rangaraj)'s
[PDB-Downloader](https://github.com/rajkumar-rangaraj/PDB-Downloader), showing how one can
programmatically obtain PDBs from Microsoft servers.
- [**@mridgers**](https://github.com/mridgers)'s
[`pdbdump.c`](https://gist.github.com/mridgers/2968595), demonstrating the use of `DbgHelp` APIs
to obtain symbols from PDBs.
- [**@m417z**](https://github.com/m417z)'s [winbindex](https://github.com/m417z/winbindex), a
database of known Windows builds and binaries.
- Microsoft for not just exporting those fkn' symbols and forcing me to write this in the first
place.
