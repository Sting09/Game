// StatModifier.cs
public class StatModifier
{
    public float Value { get; private set; }
    public StatModType Type { get; private set; }
    public object Source { get; private set; } // 记录是谁加的这个Buff

    public StatModifier(float value, StatModType type, object source = null)
    {
        Value = value;
        Type = type;
        Source = source;
    }
}