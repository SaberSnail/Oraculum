using Oraculum.Engine;

namespace Oraculum.ViewModels;

public abstract class ManualValueGeneratorViewModelBase : ValueGeneratorViewModelBase
{
	protected ManualValueGeneratorViewModelBase(RandomSourceBase source)
		: base(source)
	{
	}

	public string HintText => Source.InputHintText;
}
