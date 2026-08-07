using DashDetective.Tabs.Hardware;
using System;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>Composes a <see cref="HardwareInfoProvider"/> over canned per-card readers, so the
/// Hardware snapshot can be driven without WMI. Each section is the reader's whole <c>GetAsync</c> body,
/// which lets a test pick any of the three shapes it can complete in: a value
/// (<c>() =&gt; Task.FromResult(info)</c>), a faulted task (<c>() =&gt; Task.FromException&lt;T&gt;(e)</c>),
/// or a synchronous throw before any task exists. The last two are the contract violations the
/// composite's per-card guard exists for. An omitted section reports its <c>.Unknown</c> record.</summary>
internal static class StubHardwareInfoProviders {
    public static HardwareInfoProvider Compose(
        Func<Task<ProcessorInfo>>? processor = null,
        Func<Task<MemoryInfo>>? memory = null,
        Func<Task<StorageInfo>>? storage = null,
        Func<Task<MotherboardInfo>>? motherboard = null,
        Func<Task<GraphicsInfo>>? graphics = null) =>
        new(new StubProcessor(processor ?? (() => Task.FromResult(ProcessorInfo.Unknown))),
            new StubMemory(memory ?? (() => Task.FromResult(MemoryInfo.Unknown))),
            new StubStorage(storage ?? (() => Task.FromResult(StorageInfo.Unknown))),
            new StubMotherboard(motherboard ?? (() => Task.FromResult(MotherboardInfo.Unknown))),
            new StubGraphics(graphics ?? (() => Task.FromResult(GraphicsInfo.Unknown))));

    private sealed class StubProcessor(Func<Task<ProcessorInfo>> read) : IProcessorInfoProvider {
        public Task<ProcessorInfo> GetAsync() => read();
    }

    private sealed class StubMemory(Func<Task<MemoryInfo>> read) : IMemoryModulesProvider {
        public Task<MemoryInfo> GetAsync() => read();
    }

    private sealed class StubStorage(Func<Task<StorageInfo>> read) : IStorageInfoProvider {
        public Task<StorageInfo> GetAsync() => read();
    }

    private sealed class StubMotherboard(Func<Task<MotherboardInfo>> read) : IMotherboardInfoProvider {
        public Task<MotherboardInfo> GetAsync() => read();
    }

    private sealed class StubGraphics(Func<Task<GraphicsInfo>> read) : IGraphicsInfoProvider {
        public Task<GraphicsInfo> GetAsync() => read();
    }
}
