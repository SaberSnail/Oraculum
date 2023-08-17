namespace Oraculum.Engine;

public abstract class RandomValueBase
{
	protected RandomValueBase(int value)
	{
		Value = value;
	}

	public int Value { get; }

	public abstract string DisplayText { get; }
}
