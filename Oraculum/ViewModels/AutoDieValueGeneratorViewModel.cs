using System;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class AutoDieValueGeneratorViewModel : ValueGeneratorViewModelBase
{
	public AutoDieValueGeneratorViewModel(int config, Action onRollStarted, Action onValueGenerated)
		: base(config, onRollStarted, onValueGenerated)
	{
		MaxValue = Configuration;
		TargetValue = Configuration;
		ShouldAnimate = false;
	}

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
		GeneratedValue = m_targetValue;
		StartRoll = false;
	}

	protected override void RollCore()
	{
		TargetValue = DieUtility.GetSingleRandomValue(Configuration);
		StartRoll = false;
		StartRoll = true;
	}

	private bool m_shouldAnimate;
	private int m_targetValue;
	private bool m_startRoll;
}
