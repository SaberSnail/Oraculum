using System;

namespace Oraculum.ViewModels;

public sealed class FixedValueGeneratorViewModel : ValueGeneratorViewModelBase
{
	public FixedValueGeneratorViewModel(Action onValueGenerated)
		: base(1, onValueGenerated, 1)
	{
	}

	protected override void RollCore()
	{
	}
}
