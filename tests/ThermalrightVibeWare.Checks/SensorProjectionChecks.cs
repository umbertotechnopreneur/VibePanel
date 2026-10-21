// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;
using LibreHardwareMonitor.Hardware.Motherboard;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Core;

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
        CheckLocalBoardProfile(check);

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

    private static void CheckLocalBoardProfile(Action<bool, string> check)
    {
        const string chipId = "/lpc/nct6792d/0";
        var raw = new HardwareReading[]
        {
            new(chipId + "/temperature/1", "CPUTIN", "Temperature", 60, "°C"),
            new(chipId + "/temperature/2", "SYSTIN", "Temperature", 109, "°C"),
            new(chipId + "/temperature/3", "AUXTIN0", "Temperature", 52.5, "°C"),
            new(chipId + "/temperature/4", "AUXTIN1", "Temperature", 102, "°C"),
            new(chipId + "/temperature/17", "SMBUSMASTER0", "Temperature", 84, "°C"),
            new(chipId + "/voltage/0", "Vcore", "Voltage", 1.25, "V"),
            new(chipId + "/fan/1", "Fan #2", "Fan", 0, "RPM"),
            new(chipId + "/temperature/5", "AUXTIN2", "Temperature", 40, "°C")
        };
        var original = raw.ToArray();
        var projected = WindowsMetricsService.ProjectMotherboardReadings(Manufacturer.ASRock, Model.B550M_ITX_AC, chipId, raw);
        var temperatures = projected.Where(reading => reading.Type == "Temperature").ToArray();
        check(temperatures.SequenceEqual(new[]
        {
            raw[0] with { Name = "Mainboard" }, raw[2] with { Name = "Southbridge" }, raw[4] with { Name = "CPU" }
        }), "the local board profile publishes its selected temperature sources without changing IDs, values or units");
        check(projected.Where(reading => reading.Type != "Temperature").SequenceEqual(raw.Where(reading => reading.Type != "Temperature")),
            "the board temperature profile leaves voltage and stopped-fan readings unchanged");
        check(raw.SequenceEqual(original), "theme projection does not rename or remove Sensor Debug's raw input readings");
        var board = WindowsMetricsService.CreateMotherboard("ASRock B550M-ITX/ac",
            temperatures.Select(reading => new HardwareTemperatureSensors(reading.Name, reading.Value!.Value)).ToArray(), [], projected)!;
        check(board is { TemperatureC: 60, TemperatureLabel: "Mainboard", Temperatures.Count: 3 }
            && board.Readings!.SequenceEqual(projected),
            "the mapped Mainboard source supplies the board overview while retaining the selected detail readings");

        var edgeValues = new[] { raw[0] with { Value = 115 }, raw[2] with { Value = 0 }, raw[4] with { Value = null } };
        var edges = WindowsMetricsService.ProjectMotherboardReadings(Manufacturer.ASRock, Model.B550M_ITX_AC, chipId, edgeValues);
        check(edges.Count == 3 && edges.Select(reading => (reading.Id, reading.Value, reading.Unit))
            .SequenceEqual(edgeValues.Select(reading => (reading.Id, reading.Value, reading.Unit))),
            "source selection keeps a real high temperature, a measured zero and an unavailable value without inventing replacements");

        foreach (var (manufacturer, model, hardwareId) in new[]
        {
            (Manufacturer.ASUS, Model.B550M_ITX_AC, chipId),
            (Manufacturer.ASRock, Model.B550M_Pro4, chipId),
            (Manufacturer.ASRock, Model.B550M_ITX_AC, "/lpc/nct6793d/0"),
            (Manufacturer.ASRock, Model.B550M_ITX_AC, "/lpc/nct6792d/1")
        })
        {
            check(WindowsMetricsService.ProjectMotherboardReadings(manufacturer, model, hardwareId, raw).SequenceEqual(raw),
                "the local profile leaves other manufacturers, boards and sensor chips untouched");
        }
    }
}
