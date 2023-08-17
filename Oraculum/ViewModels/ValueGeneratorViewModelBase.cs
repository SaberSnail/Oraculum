using System;
using GoldenAnvil.Utility;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public abstract class ValueGeneratorViewModelBase : ViewModelBase
{
	public static ValueGeneratorViewModelBase Create(RandomSourceBase source, bool rollManually)
	{
		return source switch
		{
			DieSource dieSource when rollManually => new ManualDieValueGeneratorViewModel(dieSource),
			DieSource dieSource when !rollManually => new AutoDieValueGeneratorViewModel(dieSource),
			CardSource cardSource when rollManually => new ManualCardValueGeneratorViewModel(cardSource),
			CardSource cardSource when !rollManually => new AutoCardValueGeneratorViewModel(cardSource),
			_ => throw new ArgumentOutOfRangeException(),
		};
	}

	protected ValueGeneratorViewModelBase(RandomSourceBase source)
	{
		Source = source;
	}

	public event EventHandler? RollStarted;

	public event EventHandler<GenericEventArgs<RandomValueBase>>? ValueGenerated;

	public RandomSourceBase Source { get; }

	public bool IsRollStarted
	{
		get => VerifyAccess(m_isRollStarted);
		protected set
		{
			if (SetPropertyField(value, ref m_isRollStarted) && value)
			{
				GeneratedValue = null;
				RollStarted.Raise(this);
			}
		}
	}

	public RandomValueBase? GeneratedValue
	{
		get => VerifyAccess(m_generatedValue);
		protected set
		{
			if (SetPropertyField(value, ref m_generatedValue) && value is not null)
			{
				IsRollStarted = false;
				ValueGenerated.Raise(this, new GenericEventArgs<RandomValueBase>(value));
			}
		}
	}

	public void Roll()
	{
		IsRollStarted = true;
		RollCore();
	}

	public virtual void OnReportingFinished() { }

	protected virtual void RollCore() { }

	private RandomValueBase? m_generatedValue;
	private bool m_isRollStarted;
}
