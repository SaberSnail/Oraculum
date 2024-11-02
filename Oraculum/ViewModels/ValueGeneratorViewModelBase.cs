using System;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public abstract class ValueGeneratorViewModelBase : ViewModelBase
{
	public static ValueGeneratorViewModelBase Create(RandomSourceKind kind, int config, bool rollManually, Action onValueGenerated)
	{
		return kind switch
		{
			(RandomSourceKind.Fixed) => new FixedValueGeneratorViewModel(onValueGenerated),
			(RandomSourceKind.DiceSequence or RandomSourceKind.DiceSum) when rollManually =>
				new ManualDieValueGeneratorViewModel(config, onValueGenerated),
			(RandomSourceKind.DiceSequence or RandomSourceKind.DiceSum) when !rollManually =>
				new AutoDieValueGeneratorViewModel(config, onValueGenerated),
			RandomSourceKind.CardSequence when rollManually =>
				new ManualCardValueGeneratorViewModel(config, onValueGenerated),
			RandomSourceKind.CardSequence when !rollManually =>
				new AutoCardValueGeneratorViewModel(config, onValueGenerated),
			_ => throw new NotImplementedException(),
		};
	}

	protected ValueGeneratorViewModelBase(int config, Action onValueGenerated)
		: this(config, onValueGenerated, null)
	{
	}

	protected ValueGeneratorViewModelBase(int config, Action onValueGenerated, int? initialGeneratedValue)
	{
		Configuration = config;
		m_onValueGenerated = onValueGenerated;
		m_generatedValue = initialGeneratedValue;
	}

	public bool IsRollStarted
	{
		get => VerifyAccess(m_isRollStarted);
		protected set
		{
			if (SetPropertyField(value, ref m_isRollStarted) && value)
				GeneratedValue = null;
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
		RollCore();
	}

	public virtual void OnReportingFinished() { }

	protected int Configuration { get; }

	protected virtual void RollCore() => IsRollStarted = true;

	private readonly Action m_onValueGenerated;
	private int? m_generatedValue;
	private bool m_isRollStarted;
}
