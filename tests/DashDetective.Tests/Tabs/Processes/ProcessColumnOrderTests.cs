using DashDetective.Tabs.Processes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessColumnOrder"/>: the round trip through settings, that a bad or
/// stale record costs only itself, and that the pinned column comes back leftmost however the save was
/// written.</summary>
public class ProcessColumnOrderTests {
    private static readonly ProcessColumnId[] Reordered = {
        ProcessColumnId.Name, ProcessColumnId.Cpu, ProcessColumnId.Memory,
        ProcessColumnId.Pid, ProcessColumnId.Status, ProcessColumnId.Disk, ProcessColumnId.Gpu,
    };

    [Fact]
    public void EncodeThenDecode_RoundTrips() {
        Assert.Equal(Reordered, ProcessColumnOrder.Decode(ProcessColumnOrder.Encode(Reordered)));
    }

    [Fact]
    public void Encode_DropsRepeats() {
        var encoded = ProcessColumnOrder.Encode(new[] {
            ProcessColumnId.Pid, ProcessColumnId.Name, ProcessColumnId.Pid,
        });

        Assert.Equal(new[] { ProcessColumnId.Pid, ProcessColumnId.Name },
                     ProcessColumnOrder.Decode(encoded));
    }

    [Fact]
    public void Decode_EmptyOrMissing_IsEmpty() {
        Assert.Empty(ProcessColumnOrder.Decode(null));
        Assert.Empty(ProcessColumnOrder.Decode(""));
    }

    [Fact]
    public void Decode_DropsNamesNoColumnAnswersTo() {
        // A hand-edit, or a column removed in a later release: the record costs itself and nothing more.
        var encoded = ProcessColumnOrder.Encode(Reordered) + (char)0x1F + "Network";

        Assert.Equal(Reordered, ProcessColumnOrder.Decode(encoded));
    }

    [Fact]
    public void Resolve_RestoresTheSavedOrder() {
        Assert.Equal(Reordered, ProcessColumnOrder.Resolve(Reordered));
    }

    [Fact]
    public void Resolve_KeepsAColumnTheSaveNeverSawAtItsDeclaredPosition() {
        // A save written before GPU existed must not push GPU to the end — it belongs after Disk.
        IReadOnlyList<ProcessColumnId> saved = new[] {
            ProcessColumnId.Name, ProcessColumnId.Pid, ProcessColumnId.Status,
            ProcessColumnId.Cpu, ProcessColumnId.Memory, ProcessColumnId.Disk,
        };

        Assert.Equal(ProcessColumns.DefaultOrder, ProcessColumnOrder.Resolve(saved));
    }

    [Fact]
    public void Resolve_ForcesThePinnedColumnLeftmost() {
        IReadOnlyList<ProcessColumnId> saved = new[] {
            ProcessColumnId.Pid, ProcessColumnId.Name, ProcessColumnId.Status,
            ProcessColumnId.Cpu, ProcessColumnId.Memory, ProcessColumnId.Disk, ProcessColumnId.Gpu,
        };

        Assert.Equal(ProcessColumns.Pinned, ProcessColumnOrder.Resolve(saved)[0]);
    }

    [Fact]
    public void Resolve_NothingSaved_IsTheDeclaredOrder() {
        Assert.Equal(ProcessColumns.DefaultOrder, ProcessColumnOrder.Resolve(new List<ProcessColumnId>()));
    }
}
