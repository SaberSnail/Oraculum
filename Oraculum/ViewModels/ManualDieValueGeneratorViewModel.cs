using System;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class ManualDieValueGeneratorViewModel : ManualValueGeneratorViewModelBase
{
	public ManualDieValueGeneratorViewModel(int config, Action onValueGenerated)
		: base(config, onValueGenerated)
	{
	}

	public override string HintText => DieUtility.GetHintTextForConfiguration(Configuration);

	protected override (bool IsValid, string Error) IsValid(string propertyName)
	{
		if (propertyName == nameof(InputValue))
		{
			if (InputValue is null)
				return (true, "");
			if (InputValue < 1)
				return (false, OurResources.DieValueMinimumError);
			if (InputValue > Configuration)
				return (false, string.Format(OurResources.DieValueMaximumError, Configuration));
		}

		return (true, "");
	}
}
