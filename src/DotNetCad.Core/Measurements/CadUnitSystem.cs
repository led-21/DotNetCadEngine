using System.Globalization;

namespace DotNetCad.Core.Measurements;

public enum CadLinearUnit
{
    Meter,
    Centimeter,
    Millimeter
}

/// <summary>
/// Centralized CAD unit definitions, conversions, and formatting.
/// The internal geometric representation in DotNetCadEngine is always canonical meters.
/// Display units format and convert values for UI presentation without altering physical coordinates.
/// </summary>
public static class CadUnitConverter
{
    public const string UnitMeter = "m";
    public const string UnitCentimeter = "cm";
    public const string UnitMillimeter = "mm";

    public static readonly IReadOnlyList<string> SupportedUnits = Array.AsReadOnly(new[]
    {
        UnitMeter,
        UnitCentimeter,
        UnitMillimeter
    });

    public static CadLinearUnit ParseUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return CadLinearUnit.Meter;
        return unit.Trim().ToLowerInvariant() switch
        {
            "mm" or "milimetro" or "milímetros" or "millimeter" or "millimeters" => CadLinearUnit.Millimeter,
            "cm" or "centimetro" or "centímetros" or "centimeter" or "centimeters" => CadLinearUnit.Centimeter,
            _ => CadLinearUnit.Meter
        };
    }

    public static string NormalizeUnit(string? unit)
    {
        return ParseUnit(unit) switch
        {
            CadLinearUnit.Millimeter => UnitMillimeter,
            CadLinearUnit.Centimeter => UnitCentimeter,
            _ => UnitMeter
        };
    }

    public static double GetFactorToMeters(string unit)
    {
        return ParseUnit(unit) switch
        {
            CadLinearUnit.Millimeter => 0.001,
            CadLinearUnit.Centimeter => 0.01,
            _ => 1.0
        };
    }

    public static double ToMeters(double valueInDisplayUnit, string unit)
    {
        return valueInDisplayUnit * GetFactorToMeters(unit);
    }

    public static double FromMeters(double meters, string unit)
    {
        var factor = GetFactorToMeters(unit);
        return meters / factor;
    }

    public static double Convert(double value, string fromUnit, string toUnit)
    {
        var meters = ToMeters(value, fromUnit);
        return FromMeters(meters, toUnit);
    }

    public static string FormatLength(
        double meters,
        string unit,
        int decimalPlaces = 2,
        bool includeUnit = true,
        IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        decimalPlaces = Math.Clamp(decimalPlaces, 0, 8);
        var normUnit = NormalizeUnit(unit);
        var displayValue = FromMeters(meters, normUnit);
        var formatted = displayValue.ToString($"F{decimalPlaces}", provider);
        return includeUnit ? $"{formatted} {normUnit}" : formatted;
    }

    public static string FormatArea(
        double squareMeters,
        string unit,
        int decimalPlaces = 2,
        bool includeUnit = true,
        IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        decimalPlaces = Math.Clamp(decimalPlaces, 0, 8);
        var normUnit = NormalizeUnit(unit);
        var factor = GetFactorToMeters(normUnit);
        var displayValue = squareMeters / (factor * factor);
        var formatted = displayValue.ToString($"F{decimalPlaces}", provider);
        return includeUnit ? $"{formatted} {normUnit}²" : formatted;
    }

    public static string FormatAngle(
        double degrees,
        int decimalPlaces = 1,
        bool includeUnit = true,
        IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        decimalPlaces = Math.Clamp(decimalPlaces, 0, 4);
        var formatted = degrees.ToString($"F{decimalPlaces}", provider);
        return includeUnit ? $"{formatted}°" : formatted;
    }

    public static bool TryParseLength(string? text, string currentUnit, out double meters, out string error)
    {
        meters = 0.0;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Valor numérico não informado.";
            return false;
        }

        var trimmed = text.Trim();
        var targetUnit = NormalizeUnit(currentUnit);

        if (trimmed.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            targetUnit = UnitMillimeter;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            targetUnit = UnitCentimeter;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith('m') || trimmed.EndsWith('M'))
        {
            targetUnit = UnitMeter;
            trimmed = trimmed[..^1].Trim();
        }

        // Support both pt-BR comma and invariant dot
        var clean = trimmed.Replace(',', '.');
        if (double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out var num) && double.IsFinite(num))
        {
            meters = ToMeters(num, targetUnit);
            return true;
        }

        error = $"Número inválido para comprimento: '{text}'.";
        return false;
    }

    public static int GetDxfInsUnits(string unit)
    {
        return ParseUnit(unit) switch
        {
            CadLinearUnit.Millimeter => 4,
            CadLinearUnit.Centimeter => 5,
            _ => 6 // 6 = Meters
        };
    }
}
