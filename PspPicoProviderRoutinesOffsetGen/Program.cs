// See https://aka.ms/new-console-template for more information
using dnlib.PE;
using dnlib.W32Resources;
using PspPicoProviderRoutinesOffsetGen;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;

const string KernelName = "ntoskrnl.exe";
const string OutputName = "picooffsets";
const int MaxRetries = 16;

string GetVersion(PEImage module)
{
    int GetIndex<T>(IList<T> largeList, IList<T> sublist)
    {
        for (int i = 0; i < largeList.Count - sublist.Count; i++)
        {
            bool isContained = largeList.Skip(i).Take(sublist.Count).SequenceEqual(sublist);
            if (isContained) return i;
        }
        return -1;
    }

    var versionResource = module.FindWin32ResourceData(
        // RT_VERSION
        new ResourceName(16),
        // Always 1
        new ResourceName(1),
        // Kernel image so always en-US
        new ResourceName(1033)
    ).CreateReader().ToArray();

    var versionPattern = Encoding.Unicode.GetBytes("ProductVersion");

    var versionInfoIndex = GetIndex(versionResource, versionPattern);
    versionInfoIndex += versionPattern.Length;
    // Null byte.
    ++versionInfoIndex;
    // Align up 4-byte boundary.
    versionInfoIndex = (versionInfoIndex + 3) & (~3);

    return Encoding.Unicode.GetString(
        versionResource.Skip(versionInfoIndex).TakeWhile((ch, idx) =>
            ch != 0 || idx % 2 != 0).ToArray());
}

string? MachineToArchitecture(Machine? machine)
{
    return machine switch
    {
        Machine.AMD64 => "x64",
        Machine.I386 => "x86",
        Machine.ARM64 => "arm64",
        Machine.ARM => "arm",
        _ => null,
    };
}

string? GetArchitecture(PEImage module)
{
    return MachineToArchitecture(module?.ImageNTHeaders.FileHeader.Machine);
}

string? GetPdbDownloadLink(PEImage module)
{
    return module?.ImageDebugDirectories?.Where(dir => dir.Type == ImageDebugType.CodeView)
        .Select(dir =>
        {
            var reader = module.CreateReader(dir.PointerToRawData);
            var signature = reader.ReadBytes(4);
            var guid = reader.ReadGuid();
            var age = reader.ReadUInt32();
            var name = reader.TryReadZeroTerminatedString(Encoding.ASCII);
            return $"http://msdl.microsoft.com/download/symbols/" +
                name + "/" +
                guid.ToString("N").ToUpper() +
                age.ToString("X") + "/" +
                name;
        }).SingleOrDefault();
}

bool GetBinaryInfo(byte[] bytes, out string? arch, out string? ver, out string? pdbDownload)
{
    var module = new PEImage(bytes);

    arch = GetArchitecture(module);
    ver = GetVersion(module);
    pdbDownload = GetPdbDownloadLink(module);

    if (arch is null || ver is null || pdbDownload is null)
    {
        return false;
    }

    return true;
}

IEnumerable<string> GetDownloadLinks(BinaryInfo info)
{
    if (!info.Timestamp.HasValue)
    {
        yield break;
    }

    var timeDateStamp = info.Timestamp.Value;

    if (info.VirtualSize.HasValue)
    {
        yield return $"http://msdl.microsoft.com/download/symbols/" +
            KernelName + "/" +
            timeDateStamp.ToString("X8") + info.VirtualSize.Value.ToString("x") + "/" +
            KernelName;
    }
    else if (info.Size.HasValue
        && info.LastSectionPointerToRawData.HasValue
        && info.LastSectionVirtualAddress.HasValue)
    {
        const int PAGE_SIZE = 4096;

        ulong? GetMappedSize(ulong? size)
        {
            const ulong PAGE_MASK = PAGE_SIZE - 1;
            var page = size & ~PAGE_MASK;
            if (page == size) return page;
            return page + PAGE_SIZE;
        }

        // We use the rift table (VirtualAddress,PointerToRawData pairs for each section)
        // and the target file size to calculate the SizeOfImage.
        var lastSectionAndSignatureSize = info.Size - info.LastSectionPointerToRawData;
        var lastSectionAndSignatureMappedSize = GetMappedSize(
            info.LastSectionVirtualAddress + lastSectionAndSignatureSize);

        var sizeOfImage = (uint)lastSectionAndSignatureMappedSize!;
        var lowestSizeOfImage = (uint)info.LastSectionVirtualAddress + PAGE_SIZE;

        for (uint size = sizeOfImage; size >= lowestSizeOfImage; size -= PAGE_SIZE)
        {
            yield return "https://msdl.microsoft.com/download/symbols/" +
                KernelName + "/" +
                timeDateStamp.ToString("X8") + size.ToString("x") + "/" +
                KernelName;
        }
    }
}

using var client = new HttpClient();

async Task<byte[]?> GetByteArrayAsync(string? url)
{
    if (url == null)
    {
        return null;
    }
    for (int i = 0; i < MaxRetries; ++i)
    {
        try
        {
            var response = await client!.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send request to {url}: {e}");
        }
        await Task.Delay(i * 1000);
    }

    return null;
}

async Task<bool> ProbeUrlAsync(string url)
{
    for (int i = 0; i < MaxRetries; ++i)
    {
        try
        {
            var message = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client!.SendAsync(message);

            return response.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send request to {url}: {e}");
        }
        await Task.Delay(i * 1000);
    }

    return false;
}

var oldResult = new List<PspPicoProviderRoutinesOffsets>();
if (File.Exists($"{OutputName}.json"))
{
    oldResult = JsonSerializer.Deserialize<List<PspPicoProviderRoutinesOffsets>>
        (File.ReadAllText($"{OutputName}.json")) ?? oldResult;
}

var result = new List<PspPicoProviderRoutinesOffsets>();

const nint handle = 0x69420;
DbgHelp.SymInitialize(handle, null, false);

int totalIndexSize = 0;
int totalSuccess = 0;
int totalFailNull = 0;
int totalFailFile = 0;
int totalFailPdb = 0;
int totalFailInvalid = 0;
int totalOldSame = 0;
int totalOldDifferent = 0;
int totalNew = 0;

foreach (var indexArch in new[] { "arm64", "x64", "insider" })
{
    using var indexStream = await client.GetStreamAsync(
        indexArch switch
        {
            "x64" =>
                $"https://winbindex.m417z.com/data/by_filename_compressed/{KernelName}.json.gz",
            _ => $"https://m417z.com/winbindex-data-{indexArch}/" +
                    $"by_filename_compressed/{KernelName}.json.gz"
        });
    using var indexDecompressedStream = new GZipStream(indexStream, CompressionMode.Decompress);

    var index = await JsonSerializer
        .DeserializeAsync<Dictionary<string, WinbIndex>>(indexDecompressedStream);

    totalIndexSize += index!.Count;

    var processedCount = 0;

    foreach (var kvp in index!)
    {
        ++processedCount;

        Console.WriteLine($"Processing binary: {kvp.Key}");
        Console.WriteLine($"Progress: {processedCount}/{index.Count}");

        var fileInfo = kvp.Value.FileInfo;
        if (fileInfo is null)
        {
            Console.WriteLine($"Skipping null file info for file {kvp.Key}");
            ++totalFailNull;
            continue;
        }

        string? correctLink = null;
        byte[]? bytes = null;

        foreach (var link in GetDownloadLinks(fileInfo))
        {
            if (await ProbeUrlAsync(link))
            {
                correctLink = link;
                break;
            }
        }

        if (correctLink != null)
        {
            Console.WriteLine($"Downloading binary from {correctLink}");
            bytes = await GetByteArrayAsync(correctLink);
        }

        if (bytes == null)
        {
            Console.WriteLine($"Cannot download file {kvp.Key}");
            ++totalFailFile;
            continue;
        }

        if (GetBinaryInfo(bytes, out var arch, out var ver, out var pdbDownload))
        {
            Console.WriteLine($"Version: {ver}, Architecture: {arch}");

            if (fileInfo.Version is not null && !fileInfo.Version.StartsWith(ver!))
            {
                Console.WriteLine($"Metadata for file {kvp.Key} reports inconsistent versions.");
                Console.WriteLine($"From database: {fileInfo.Version}");
                Console.WriteLine($"From PE: {ver}");
            }

            if (MachineToArchitecture((Machine)fileInfo.MachineType) != arch)
            {
                Console
                    .WriteLine($"Metadata for file {kvp.Key} reports inconsistent architectures.");
                Console.WriteLine($"From database: {fileInfo.MachineType}");
                Console.WriteLine($"From PE: {arch}");
            }

            var pdbName = new Uri(pdbDownload!).Segments.Last();
            Console.WriteLine($"Downloading PDB from {pdbDownload}");

            try
            {
                var pdbBytes = await GetByteArrayAsync(pdbDownload);
                // pdbBytes may be null of course, but in that case we would also want to crash
                // into that catch block.
                File.WriteAllBytes(pdbName, pdbBytes!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot download PDB for file {kvp.Key}");
                Console.WriteLine(ex);
                ++totalFailPdb;
                continue;
            }

            ulong baseAddr = 0x400000;

            baseAddr = DbgHelp.SymLoadModuleEx(handle, IntPtr.Zero, pdbName, "nt",
                baseAddr, (uint)bytes.Length, IntPtr.Zero, 0);

            var offsets = new PspPicoProviderRoutinesOffsets()
            {
                Architecture = arch!,
                Version = ver!
            };

            // Boxing. Otherwise SetValue would not work.
            object values = offsets.Offsets;

            DbgHelp.SymEnumSymbols(handle, baseAddr, "Ps*", (sym, ctx) =>
            {
                var propertyInfo = values.GetType().GetProperty(sym.Name);
                if (propertyInfo is not null)
                {
                    var offset = sym.Address - sym.ModBase;
                    Console.WriteLine($"{sym.Name}: {offset:X}");
                    propertyInfo.SetValue(values, offset);
                }

                return true;
            }, IntPtr.Zero);

            // Unboxing before adding
            offsets.Offsets = (PspPicoProviderRoutinesOffsets.Values)values;

            result.Add(offsets);

            DbgHelp.SymUnloadModule(handle, baseAddr);

            Console.WriteLine($"Processed binary: {kvp.Key}");
            File.Delete(pdbName);

            ++totalSuccess;
        }
        else
        {
            Console.WriteLine($"Skipping invalid binary: {kvp.Key}");
            ++totalFailInvalid;
        }
    }
}

// Do this to make the diffs less messy.
foreach (var res in result)
{
    var similarOld = oldResult.FirstOrDefault(old =>
    {
        return old.Architecture == res.Architecture
            && old.Version == res.Version
            && old.Offsets.Equals(res.Offsets);
    });

    if (similarOld != null)
    {
        Console.WriteLine($"Skipping {res.Architecture} build for {res.Version}...");
        ++totalOldSame;
        continue;
    }

    var conflictingOld = oldResult.FirstOrDefault(old =>
    {
        return old.Architecture == res.Architecture
            && old.Version == res.Version;
    });

    if (conflictingOld != null)
    {
        conflictingOld.Offsets = res.Offsets;
        ++totalOldDifferent;
        continue;
    }

    oldResult!.Add(res);
    ++totalNew;
}

result = oldResult;

File.WriteAllBytes($"{OutputName}.json", JsonSerializer.SerializeToUtf8Bytes(result));

var headerCodeWriter = new StringWriter();
headerCodeWriter.WriteLine("#pragma once");
headerCodeWriter.WriteLine();
headerCodeWriter.WriteLine("// picooffests.h");
headerCodeWriter.WriteLine("//");
headerCodeWriter
    .WriteLine("// Generated offsets for important symbols supporting Pico providers.");
headerCodeWriter.WriteLine();
headerCodeWriter.WriteLine("#include <ntddk.h>");
headerCodeWriter.WriteLine();
headerCodeWriter.WriteLine(
@"typedef struct _MA_PSP_PICO_PROVIDER_ROUTINES_OFFSETS {
    PCSTR Version;
    PCSTR Architecture;
    struct {");
foreach (var property in typeof(PspPicoProviderRoutinesOffsets.Values).GetProperties())
{
    headerCodeWriter.WriteLine($"        ULONG64 {property.Name};");
}
headerCodeWriter.WriteLine(
@"    }} Offsets;
}} MA_PSP_PICO_PROVIDER_ROUTINES_OFFSETS, *PMA_PSP_PICO_PROVIDER_ROUTINES_OFFSETS;

extern const MA_PSP_PICO_PROVIDER_ROUTINES_OFFSETS MaPspPicoProviderRoutinesOffsets[{0}];
", totalIndexSize);

var headerCode = headerCodeWriter.ToString();

File.WriteAllText($"{OutputName}.h", headerCode);

var cppCodeWriter = new StringWriter();
cppCodeWriter.WriteLine(
@"#include ""picooffsets.h""

extern const MA_PSP_PICO_PROVIDER_ROUTINES_OFFSETS MaPspPicoProviderRoutinesOffsets[] =
{");

foreach (var offsets in result)
{
    cppCodeWriter.WriteLine($"    {{");
    cppCodeWriter.WriteLine($"        .Version = \"{offsets.Version}\",");
    cppCodeWriter.WriteLine($"        .Architecture = \"{offsets.Architecture}\",");
    cppCodeWriter.WriteLine($"        .Offsets =");
    cppCodeWriter.WriteLine($"        {{");

    foreach (var property in offsets.Offsets.GetType().GetProperties())
    {
        var offset = (ulong)property.GetValue(offsets.Offsets)!;
        if (offset != 0)
        {
            cppCodeWriter.WriteLine($"            .{property.Name} = 0x{offset:X},");
        }
    }

    cppCodeWriter.WriteLine($"        }},");
    cppCodeWriter.WriteLine($"    }},");
}

cppCodeWriter.WriteLine("};");
cppCodeWriter.WriteLine();

var cppCode = cppCodeWriter.ToString();

File.WriteAllText($"{OutputName}.cpp", cppCode);

Console.WriteLine();
Console.WriteLine("STATISTICS:");
Console.WriteLine($"Index:                  {totalIndexSize} entries.");
Console.WriteLine($"Succeeded:              {totalSuccess} entries.");
Console.WriteLine($"Failed (no info):       {totalFailNull} entries.");
Console.WriteLine($"Failed (no exe):        {totalFailFile} entries.");
Console.WriteLine($"Failed (no pdb):        {totalFailPdb} entries.");
Console.WriteLine($"Failed (invalid):       {totalFailInvalid} entries.");
Console.WriteLine($"Unchanged/Duplicate:    {totalOldSame} entries.");
Console.WriteLine($"Updated:                {totalOldDifferent} entries.");
Console.WriteLine($"Added:                  {totalNew} entries.");
Console.WriteLine();
