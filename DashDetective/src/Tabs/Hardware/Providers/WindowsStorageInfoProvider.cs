using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Windows;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Drive facts, one row per physical disk. Primary source is <c>MSFT_PhysicalDisk</c>
/// (<c>root\Microsoft\Windows\Storage</c>), which gives the friendly model, size, media/bus type
/// (SSD/HDD/NVMe) and health in one place. If that namespace is unavailable it falls back to
/// <c>Win32_DiskDrive</c> for model + size only (type/health then read "—"). The platform check lives in
/// <see cref="IHardwareInfoProvider.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsStorageInfoProvider : IStorageInfoProvider {
    private const string StorageScope = @"\\.\root\Microsoft\Windows\Storage";

    public Task<StorageInfo> GetAsync() => Task.Run(Read);

    private static StorageInfo Read() {
        try {
            var devices = new List<StorageDeviceInfo>();
            var healthCodes = new List<int>();
            ulong totalBytes = 0;
            var haveHealth = false;

            // Primary: the Storage-management namespace (model + size + type + health).
            try {
                WmiRead.ForEach(StorageScope,
                    "SELECT FriendlyName, Size, MediaType, BusType, HealthStatus FROM MSFT_PhysicalDisk",
                    obj => {
                        var bytes = WmiRead.ToUInt64(obj["Size"]);
                        var type = StorageSpecFormatter.TypeLabel(
                            WmiRead.ToInt(obj["MediaType"]), WmiRead.ToInt(obj["BusType"]));
                        totalBytes += bytes;
                        devices.Add(new StorageDeviceInfo(
                            ModelOrDefault(WmiRead.Text(obj, "FriendlyName")),
                            StorageSpecFormatter.DriveDetail(bytes, type)));
                        healthCodes.Add(WmiRead.ToInt(obj["HealthStatus"]));
                        haveHealth = true;
                    });
            } catch (Exception e) {
                // Storage namespace unavailable — discard any partial read and fall back below.
                Log.Warn("StorageInfoProvider MSFT_PhysicalDisk read failed, falling back", e);
                devices.Clear();
                healthCodes.Clear();
                totalBytes = 0;
                haveHealth = false;
            }

            // Fallback: classic Win32_DiskDrive (no media type or health).
            if (devices.Count == 0) {
                WmiRead.ForEach("SELECT Model, Size FROM Win32_DiskDrive", obj => {
                    var bytes = WmiRead.ToUInt64(obj["Size"]);
                    totalBytes += bytes;
                    devices.Add(new StorageDeviceInfo(
                        ModelOrDefault(WmiRead.Text(obj, "Model")),
                        StorageSpecFormatter.DriveDetail(bytes, "")));
                });
            }

            if (devices.Count == 0)
                return StorageInfo.Unknown;

            return new StorageInfo(
                Summary: StorageSpecFormatter.Summary(devices.Count, totalBytes),
                Drives: devices,
                TotalHealth: haveHealth ? StorageSpecFormatter.Health(healthCodes) : "—");
        } catch (Exception e) {
            Log.Warn("StorageInfoProvider read failed", e);
            return StorageInfo.Unknown;
        }
    }

    private static string ModelOrDefault(string model) =>
        string.IsNullOrWhiteSpace(model) ? "Drive" : model;
}

/// <summary>The no-drives contract, until the Linux storage milestone lands its reader.</summary>
internal sealed class UnsupportedStorageInfoProvider : IStorageInfoProvider {
    public Task<StorageInfo> GetAsync() => Task.FromResult(StorageInfo.Unknown);
}
