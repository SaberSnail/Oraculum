using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class ManualCardValueGeneratorViewModel : ManualValueGeneratorViewModelBase
{
	public ManualCardValueGeneratorViewModel(CardSource source)
		: base(source)
	{
		Source = source;
	}

	public new CardSource Source { get; }
}
