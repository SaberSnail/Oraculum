using System;

namespace Oraculum.ViewModels;

public sealed class FixedValueGeneratorViewModel : ValueGeneratorViewModelBase
{
	public FixedValueGeneratorViewModel(Action onValueGenerated)
		: base(1, onValueGenerated)
	{
	}

	protected override void RollCore()
	{
		GeneratedValue = 1;
	}
}
