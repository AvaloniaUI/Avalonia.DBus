namespace Atspi2TestApp;

internal sealed class ValueInfo
{
    public ValueInfo(double minimum, double maximum, double current, double increment, string text)
    {
        Minimum = minimum;
        Maximum = maximum;
        Current = current;
        Increment = increment;
        Text = text;
    }

    public double Minimum { get; }
    public double Maximum { get; }
    public double Current { get; set; }
    public double Increment { get; }
    public string Text { get; }
}
