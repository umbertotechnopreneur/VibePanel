// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;
using VibePanel.Contracts;
using VibePanel.Core;

internal static class SensorProjectionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var reported = new HardwareTemperatureSensors[]
        {
            new("Temperature #1", 42), new("VRM", 52), new("CPU Socket", 48)
        };
        var fans = new HardwareFanSensors[] { new("Fan #1", 0), new("Chassis Fan", 950) };
        var board = WindowsMetricsService.CreateMotherboard("Fixture board", reported, fans)!;
        check(board.Temperatures!.SequenceEqual(reported), "generic, VRM and socket temperatures retain their reported names");
        check(board.TemperatureC is null && board.TemperatureLabel is null,
            "an unidentified or socket sensor is not relabeled as motherboard temperature");
        check(board.Fans.SequenceEqual(fans) && board.Fans[0].Rpm == 0,
            "generic board fans keep their names and a measured stop remains zero");
        var identity = WindowsMetricsService.CreateMotherboard("Fixture board", [], []);
        check(identity is { Name: "Fixture board", TemperatureC: null, Fans.Count: 0 },
            "board identity is preserved when the sensor library supplies no readings");
        check(WindowsMetricsService.CreateMotherboard(null, [], []) is null,
            "a missing motherboard is not invented");
        var named = WindowsMetricsService.CreateMotherboard("Fixture board",
            [new("Temperature #1", 60), new("SYS", 35), new("Motherboard", 37)], [])!;
        check(named is { TemperatureC: 37, TemperatureLabel: "Motherboard", Temperatures.Count: 3 },
            "a trusted board label supplies the legacy primary temperature without dropping the other readings");

        var unavailable = WindowsMetricsService.AvailabilityNotice(new(false, "fixture.sys"), null, identity, false);
        check(unavailable is not null && unavailable.Contains("PawnIO is not installed", StringComparison.Ordinal)
            && unavailable.Contains("GPU and RAM", StringComparison.Ordinal),
            "a missing PawnIO installation explains which sensor groups may be missing");
        var partial = WindowsMetricsService.AvailabilityNotice(new(true, "fixture.sys"),
            new("CPU", 20, null, null), identity, false);
        check(partial is not null && partial.Contains("CPU temperature", StringComparison.Ordinal)
            && partial.Contains("motherboard temperatures", StringComparison.Ordinal)
            && partial.Contains("fan RPM", StringComparison.Ordinal),
            "a registered driver does not hide missing CPU, motherboard and fan measurements");
        var complete = WindowsMetricsService.AvailabilityNotice(new(true, "fixture.sys"),
            new("CPU", 20, 4000, 55), board, false);
        check(complete is null, "available named board readings and fans do not produce a false missing-data warning");

        var snapshot = new HardwareSensorSnapshot(null, [], null, null, Motherboard: board,
            Driver: new(false, "fixture.sys", "Fixture driver failure"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, PanelContract.Json));
        check(json.RootElement.GetProperty("motherboard").GetProperty("temperatures").GetArrayLength() == 3
            && !json.RootElement.GetProperty("driver").GetProperty("available").GetBoolean(),
            "theme JSON carries named board temperatures and the existing driver's availability");

        using var reader = new WindowsMetricsService();
        check(!reader.DriverStatus.Available && reader.DriverStatus.Notice!.Contains("not been checked", StringComparison.Ordinal),
            "reading cached driver status does not initialize or sample hardware");
    }
}
