using DashDetective.Services.Diagnostics;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Reads an NVMe drive's composite temperature via the in-box <c>IOCTL_STORAGE_QUERY_PROPERTY</c> health-log
/// query — no admin, no dependency, and only for NVMe drives (SATA/HDD/USB return <c>null</c>). Opens
/// <c>\\.\PhysicalDriveN</c> with zero access (enough for the read-only property query, so no elevation), asks
/// for NVMe SMART/Health log page 0x02, and parses the composite temperature (Kelvin) into Celsius. Every
/// failure — non-Windows, non-NVMe, unsupported, access denied, implausible reading — soft-fails to
/// <c>null</c> rather than throwing.
/// </summary>
public static class DiskTemperatureProvider {
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageDeviceProtocolSpecificProperty = 50; // STORAGE_PROPERTY_ID
    private const int PropertyStandardQuery = 0;                   // STORAGE_QUERY_TYPE
    private const int ProtocolTypeNvme = 3;                        // STORAGE_PROTOCOL_TYPE
    private const uint NVMeDataTypeLogPage = 2;                    // STORAGE_PROTOCOL_NVME_DATA_TYPE
    private const uint NVMeHealthInfoLogPage = 0x02;              // SMART / Health Information log

    private const uint GenericNone = 0;
    private const uint FileShareReadWrite = 0x1 | 0x2;
    private const uint OpenExisting = 3;

    // Buffer layout: 8-byte query/descriptor header + 40-byte STORAGE_PROTOCOL_SPECIFIC_DATA + 512-byte log.
    private const int HeaderSize = 8;
    private const int ProtocolDataSize = 40;
    private const int LogSize = 512;
    private const int ProtocolDataOffset = HeaderSize + ProtocolDataSize;
    private const int BufferSize = ProtocolDataOffset + LogSize;

    // Plausible drive-temperature window; anything outside means "no real reading".
    private const double MinCelsius = 1;
    private const double MaxCelsius = 125;
    private const double KelvinOffset = 273;

    /// <summary>NVMe composite temperature in °C for physical drive <paramref name="deviceId"/>, or
    /// <c>null</c> when unavailable (non-NVMe, unsupported, or any failure).</summary>
    public static double? ReadCelsius(int deviceId) {
        if (!OperatingSystem.IsWindows())
            return null;

        try {
            using var handle = CreateFileW(
                $@"\\.\PhysicalDrive{deviceId}", GenericNone, FileShareReadWrite,
                IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
                return null;

            var buffer = new byte[BufferSize];
            // STORAGE_PROPERTY_QUERY header.
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), StorageDeviceProtocolSpecificProperty);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), PropertyStandardQuery);
            // STORAGE_PROTOCOL_SPECIFIC_DATA (starts at the header's AdditionalParameters).
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), ProtocolTypeNvme);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12), NVMeDataTypeLogPage);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), NVMeHealthInfoLogPage);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24), ProtocolDataSize); // ProtocolDataOffset
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(28), LogSize);          // ProtocolDataLength

            if (!DeviceIoControl(
                    handle, IoctlStorageQueryProperty, buffer, (uint)buffer.Length,
                    buffer, (uint)buffer.Length, out _, IntPtr.Zero))
                return null;

            // Health log byte 0 is Critical Warning; bytes 1-2 are Composite Temperature in Kelvin.
            ushort kelvin = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(ProtocolDataOffset + 1));
            return KelvinToCelsius(kelvin);
        } catch (Exception e) {
            Log.Warn($"DiskTemperatureProvider read failed (drive {deviceId})", e);
            return null;
        }
    }

    /// <summary>Converts an NVMe composite temperature (Kelvin) to °C, returning <c>null</c> when the value
    /// is outside a plausible drive range (e.g. 0 Kelvin = "not reported").</summary>
    internal static double? KelvinToCelsius(ushort kelvin) {
        double celsius = kelvin - KelvinOffset;
        return celsius is >= MinCelsius and <= MaxCelsius ? celsius : null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode, byte[] lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
}
