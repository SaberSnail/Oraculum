using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;
using Oraculum.Data;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class ValueGenerator : ViewModelBase, IDisposable
{
	public ValueGenerator(TableReference tableReference, RandomSourceBase randomSource, Action<RandomValueBase> onValueGenerated)
	{
		m_tableReference = tableReference;
		m_onValueGenerated = onValueGenerated;
		m_randomSource = randomSource;
		m_generators = CreateGenerators();
		AppModel.Instance.Settings.SettingChanged += OnSettingChanged;
	}

	public IReadOnlyList<ValueGeneratorViewModelBase> Generators
	{
		get => VerifyAccess(m_generators);
		set => SetPropertyField(value, ref m_generators);
	}

	public void Roll()
	{
		foreach (var generator in m_generators)
			generator.Roll();
	}

	public void Dispose()
	{
		AppModel.Instance.Settings.SettingChanged -= OnSettingChanged;
	}

	private IReadOnlyList<ValueGeneratorViewModelBase> CreateGenerators()
	{
		var rollManually = AppModel.Instance.Settings.Get<bool>(SettingsKeys.RollValueManually);
		return m_randomSource.Configurations
			.Select(x => ValueGeneratorViewModelBase.Create(m_randomSource.Kind, x, rollManually, OnRollStarted, OnValueGenerated))
			.AsReadOnlyList();
	}

	private void OnRollStarted()
	{
		if (m_generators.Count(x => x.IsRollStarted) == 1)
			AppModel.Instance.RollLog.RollStarted(m_tableReference);
	}

	private void OnValueGenerated()
	{
		if (m_generators.All(x => x.GeneratedValue is not null))
		{
			var values = m_generators.Select(x => x.GeneratedValue!.Value).AsReadOnlyList();
			RandomValueBase value = m_randomSource switch
			{
				DiceSumSource dieSource => new DieValue(values.Sum()),
				DiceSequenceSource dieSource => new DieValue(values),
				CardSequenceSource cardSource => new CardValue(values),
				_ => throw new NotImplementedException(),
			};

			m_onValueGenerated(value);

			foreach (var generator in m_generators)
				generator.OnReportingFinished();
		}
	}

	private void OnSettingChanged(object? sender, GenericEventArgs<string> e)
	{
		if (e.Value == SettingsKeys.RollValueManually)
			Generators = CreateGenerators();
	}

	private readonly TableReference m_tableReference;
	private readonly Action<RandomValueBase> m_onValueGenerated;
	private readonly RandomSourceBase m_randomSource;
	private IReadOnlyList<ValueGeneratorViewModelBase> m_generators;
}
