using System;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public abstract class ValueGeneratorViewModelBase : ViewModelBase
{
	public static ValueGeneratorViewModelBase Create(RandomSourceKind kind, int config, bool rollManually, Action onRollStarted, Action onValueGenerated)
	{
		return kind switch
		{
			(RandomSourceKind.DiceSequence or RandomSourceKind.DiceSum) when rollManually =>
				new ManualDieValueGeneratorViewModel(config, onRollStarted, onValueGenerated),
			(RandomSourceKind.DiceSequence or RandomSourceKind.DiceSum) when !rollManually =>
				new AutoDieValueGeneratorViewModel(config, onRollStarted, onValueGenerated),
			RandomSourceKind.CardSequence when rollManually =>
				new ManualCardValueGeneratorViewModel(config, onRollStarted, onValueGenerated),
			RandomSourceKind.CardSequence when !rollManually =>
				new AutoCardValueGeneratorViewModel(config, onRollStarted, onValueGenerated),
			_ => throw new NotImplementedException(),
		};
	}

	protected ValueGeneratorViewModelBase(int config, Action onRollStarted, Action onValueGenerated)
	{
		Configuration = config;
		m_onRollStarted = onRollStarted;
		m_onValueGenerated = onValueGenerated;
	}

	public bool IsRollStarted
	{
		get => VerifyAccess(m_isRollStarted);
		protected set
		{
			if (SetPropertyField(value, ref m_isRollStarted) && value)
			{
				GeneratedValue = null;
				m_onRollStarted();
			}
		}
	}

	public int? GeneratedValue
	{
		get => VerifyAccess(m_generatedValue);
		protected set
		{
			if (SetPropertyField(value, ref m_generatedValue) && value is not null)
			{
				IsRollStarted = false;
				m_onValueGenerated();
			}
		}
	}

	public void Roll()
	{
		IsRollStarted = true;
		RollCore();
	}

	public virtual void OnReportingFinished() { }

	protected int Configuration { get; }

	protected virtual void RollCore() { }

	private readonly Action m_onRollStarted;
	private readonly Action m_onValueGenerated;
	private int? m_generatedValue;
	private bool m_isRollStarted;
}
