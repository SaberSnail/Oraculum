using System;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class AutoCardValueGeneratorViewModel : ValueGeneratorViewModelBase
{
	public AutoCardValueGeneratorViewModel(int config, Action onRollStarted, Action onValueGenerated)
		: base(config, onRollStarted, onValueGenerated)
	{
	}

	protected override void RollCore()
	{
		GeneratedValue = DieUtility.GetSingleRandomValue(Configuration);
	}
}
