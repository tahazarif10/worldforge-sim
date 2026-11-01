using System;
using System.Collections.Generic;
using System.Globalization;

namespace WorldForge.Instrumentation;

/// <summary>Opt-in fixture configuration, independent of Godot and the filesystem.</summary>
public sealed record AcceptanceOptions(string Scenario, string Session,
    string Output, double DurationSeconds, double WarmupSeconds, string Quality)
{
    public const string FixtureVersion = "v3.3-reference-1";
    public const float CameraX = 60f, CameraZ = 44f, CameraSize = 79f;

    public static AcceptanceOptions? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string arg in args)
        {
            if (!arg.StartsWith("--acceptance-", StringComparison.Ordinal)) continue;
            int separator = arg.IndexOf('=');
            if (separator < 0 || separator == arg.Length - 1)
                throw new ArgumentException("Acceptance flags require a nonempty =value.");
            string key = arg[13..separator];
            if (key is not ("scenario" or "session" or "output" or "duration" or "warmup" or "quality"))
                throw new ArgumentException("Unknown acceptance flag: " + key);
            if (!values.TryAdd(key, arg[(separator + 1)..]))
                throw new ArgumentException("Duplicate acceptance flag: " + key);
        }
        if (values.Count == 0) return null;
        string Required(string key) => values.TryGetValue(key, out string? value)
            ? value : throw new ArgumentException("Missing --acceptance-" + key);
        string scenario = Required("scenario");
        if (scenario is not ("idle" or "pan" or "zoom" or "paint"))
            throw new ArgumentException("Scenario must be idle, pan, zoom or paint.");
        string session = Required("session");
        FrameCapture.ValidateId(session);
        string quality = values.GetValueOrDefault("quality", "Eco");
        if (quality is not ("Eco" or "Balanced"))
            throw new ArgumentException("Quality must be Eco or Balanced.");
        double Number(string key, double fallback, double min, double max)
        {
            if (!values.TryGetValue(key, out string? value)) return fallback;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ||
                !double.IsFinite(n) || n < min || n > max)
                throw new ArgumentException("Invalid acceptance " + key);
            return n;
        }
        return new AcceptanceOptions(scenario, session, Required("output"),
            Number("duration", scenario == "pan" ? 60 : 30, .1, 180),
            Number("warmup", 10, 0, 60), quality);
    }

    // The trace is a continuous function of elapsed monotonic seconds, sampled
    // at actual callback times. Raw pose traces expose sampling differences.
    public static (float X, float Y) PanOffset(double seconds) =>
        (160f * (float)Math.Sin(seconds * Math.PI / 3),
          90f * (float)Math.Sin(seconds * Math.PI / 6));

    public static float ZoomSize(double seconds) =>
        CameraSize * (float)Math.Pow(1.14, 4 * Math.Sin(seconds * Math.PI / 4));

    public static (int X, int Y, bool Forest) PaintAction(int index) =>
        (60 + index % 9, 40 + index / 9 % 9, index % 2 == 0);
}
