using System.ComponentModel;
using GoldenAnvil.Utility.Windows;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class ManualDieValueGeneratorViewModel : ManualValueGeneratorViewModelBase, IDataErrorInfo
{
	public ManualDieValueGeneratorViewModel(DieSource source)
		: base(source)
	{
		Source = source;
	}

	public new DieSource Source { get; }

	public int? Value
	{
		get => VerifyAccess(m_value);
		set
		{
			if (SetPropertyField(value, ref m_value) && IsValid(nameof(Value)).IsValid && m_value is not null)
				GeneratedValue = new DieValue(value!.Value);
		}
	}

	public string Error => "";

	public string this[string propertyName] => IsValid(propertyName).Error;

	public override void OnReportingFinished() => Roll();

	protected override void RollCore() => Value = null;

	private (bool IsValid, string Error) IsValid(string propertyName)
	{
		if (propertyName == nameof(Value))
		{
			if (Value is null)
				return (true, "");
			if (Value < 1)
				return (false, OurResources.DieValueMinimumError);
			if (Value > Source.Sides)
				return (false, string.Format(OurResources.DieValueMaximumError, Source.Sides));
		}

		return (true, "");
	}

	private int? m_value;
}
