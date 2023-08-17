using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class AutoDieValueGeneratorViewModel : ValueGeneratorViewModelBase
{
	public AutoDieValueGeneratorViewModel(DieSource source)
		: base(source)
	{
		Source = source;
		MaxValue = source.Sides;
		TargetValue = source.Sides;
		ShouldAnimate = true;
	}

	public new DieSource Source { get; }

	public int MaxValue { get; }

	public int TargetValue
	{
		get => VerifyAccess(m_targetValue);
		set => SetPropertyField(value, ref m_targetValue);
	}

	public bool ShouldAnimate
	{
		get => VerifyAccess(m_shouldAnimate);
		private set => SetPropertyField(value, ref m_shouldAnimate);
	}

	public bool StartRoll
	{
		get => VerifyAccess(m_startRoll);
		private set => SetPropertyField(value, ref m_startRoll);
	}

	public void OnTargetValueDisplayed()
	{
		GeneratedValue = new DieValue(m_targetValue);
		StartRoll = false;
	}

	protected override void RollCore()
	{
		var value = Source.GetRandomValue();
		TargetValue = value.Value;
		StartRoll = false;
		StartRoll = true;
	}

	private bool m_shouldAnimate;
	private int m_targetValue;
	private bool m_startRoll;
}
