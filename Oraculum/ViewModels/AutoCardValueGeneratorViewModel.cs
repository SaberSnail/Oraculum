using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class AutoCardValueGeneratorViewModel : ValueGeneratorViewModelBase
{
	public AutoCardValueGeneratorViewModel(CardSource source)
		: base(source)
	{
		Source = source;
	}

	public new CardSource Source { get; }
}
