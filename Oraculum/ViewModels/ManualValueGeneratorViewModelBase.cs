using System;
using System.ComponentModel;

namespace Oraculum.ViewModels;

public abstract class ManualValueGeneratorViewModelBase : ValueGeneratorViewModelBase, IDataErrorInfo
{
	protected ManualValueGeneratorViewModelBase(int config, Action onRollStarted, Action onValueGenerated)
		: base(config, onRollStarted, onValueGenerated)
	{
	}

	public int? InputValue
	{
		get => VerifyAccess(m_inputValue);
		set
		{
			if (SetPropertyField(value, ref m_inputValue) && IsValid(nameof(InputValue)).IsValid && m_inputValue is not null)
				GeneratedValue = value!.Value;
		}
	}

	public string Error => "";

	public string this[string propertyName] => IsValid(propertyName).Error;

	public override void OnReportingFinished() => Roll();

	protected override void RollCore() => InputValue = null;

	protected abstract (bool IsValid, string Error) IsValid(string propertyName);

	private int? m_inputValue;
}
