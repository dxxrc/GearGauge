namespace GearGauge.Core.Models;

public readonly record struct OptionalFloat
{
    public static OptionalFloat None => new(null);

    public OptionalFloat(float? value)
    {
        Value = value.HasValue && !float.IsFinite(value.Value)
            ? null
            : value;
    }

    public float? Value { get; }

    public bool HasValue => Value.HasValue;

    public static implicit operator OptionalFloat(float value) => new(value);

    public static implicit operator OptionalFloat(float? value) => new(value);

    public override string ToString() => HasValue ? Value!.Value.ToString("0.##") : "N/A";
}
