using DashDetective.Services.Diagnostics;
using System;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Composes the Hardware tab's snapshot from one reader per card. Each reader owns its own queries and its
/// own soft-fail, so the five run <b>concurrently</b> and the card set populates in about the time of the
/// slowest read rather than their sum. <see cref="Section"/> guards each await, so a reader that throws
/// instead of honouring the never-throw contract costs only its own card — one dead source still can't
/// blank the others.
///
/// <b>The composition itself is portable</b>, which is why this carries no platform in its name and no
/// <see cref="System.Runtime.Versioning.SupportedOSPlatformAttribute"/>: resolving the readers was the only
/// Windows-specific part, and that now lives in <see cref="IHardwareInfoProvider.ForCurrentPlatform"/>
/// alongside the Linux set. Adding a platform is one more factory there and a reader per card it can
/// supply — cards it cannot keep their <c>Unsupported*</c> reader.
/// </summary>
internal sealed class HardwareInfoProvider : IHardwareInfoProvider {
    private readonly IProcessorInfoProvider _processor;
    private readonly IMemoryModulesProvider _memory;
    private readonly IStorageInfoProvider _storage;
    private readonly IMotherboardInfoProvider _motherboard;
    private readonly IGraphicsInfoProvider _graphics;

    internal HardwareInfoProvider(
        IProcessorInfoProvider processor, IMemoryModulesProvider memory, IStorageInfoProvider storage,
        IMotherboardInfoProvider motherboard, IGraphicsInfoProvider graphics) {
        _processor = processor;
        _memory = memory;
        _storage = storage;
        _motherboard = motherboard;
        _graphics = graphics;
    }

    public async Task<HardwareInfo> GetAsync() {
        // Each Section runs its reader synchronously up to that reader's first await, so all five queries
        // are in flight before the WhenAll below.
        var processor = Section("Processor", _processor.GetAsync, ProcessorInfo.Unknown);
        var memory = Section("Memory", _memory.GetAsync, MemoryInfo.Unknown);
        var storage = Section("Storage", _storage.GetAsync, StorageInfo.Unknown);
        var motherboard = Section("Motherboard", _motherboard.GetAsync, MotherboardInfo.Unknown);
        var graphics = Section("Graphics", _graphics.GetAsync, GraphicsInfo.Unknown);

        await Task.WhenAll(processor, memory, storage, motherboard, graphics);

        return new HardwareInfo(
            processor.Result, memory.Result, storage.Result, motherboard.Result, graphics.Result);
    }

    /// <summary>Runs one card's read, falling back to its <c>.Unknown</c> if the reader throws — without
    /// this, a single faulted task would take <c>Task.WhenAll</c> and every other card down with it. The
    /// reader is <b>invoked</b> inside the try, not just awaited, so a reader that throws synchronously
    /// before returning its task is caught too.</summary>
    private static async Task<T> Section<T>(string card, Func<Task<T>> read, T unknown) {
        try {
            return await read();
        } catch (Exception e) {
            Log.Warn($"HardwareInfoProvider {card} section failed", e);
            return unknown;
        }
    }
}

/// <summary>The no-inventory set: every card reports <c>.Unknown</c> and so renders "—", which is what
/// the old <c>OperatingSystem.IsWindows()</c> guard returned off Windows. Used on a platform with no
/// reader for any card at all.</summary>
internal sealed class UnsupportedHardwareInfoProvider : IHardwareInfoProvider {
    public Task<HardwareInfo> GetAsync() => Task.FromResult(HardwareInfo.Unknown);
}
