namespace Oraculum.Engine;

public sealed class DieValue : RandomValueBase
{
	public DieValue(int value)
		: base(value)
	{
		DisplayText = value.ToString();
	}

	public override string DisplayText { get; }
}
