using System;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class AutoCardValueGeneratorViewModel : ValueGeneratorViewModelBase
{
	public AutoCardValueGeneratorViewModel(int config, Action onValueGenerated)
		: base(config, onValueGenerated)
	{
	}

	protected override void RollCore()
	{
		GeneratedValue = DieUtility.GetSingleRandomValue(Configuration);
	}
}
