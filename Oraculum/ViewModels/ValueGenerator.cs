using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;
using Oraculum.Data;
using Oraculum.Engine;

namespace Oraculum.ViewModels;

public sealed class ValueGenerator : ViewModelBase, IDisposable
{
	public ValueGenerator(IEnumerable<RandomSourceBase> randomSources, Action<IReadOnlyList<RandomValueBase>> onValueGenerated)
	{
		m_onValueGenerated = onValueGenerated;
		m_randomSources = randomSources.ToList();
		m_randomSourcesAndGenerators = [];
		m_allGenerators = CreateGenerators();
		AppModel.Instance.Settings.SettingChanged += OnSettingChanged;
	}

	public IReadOnlyList<ValueGeneratorViewModelBase> Generators
	{
		get => VerifyAccess(m_allGenerators);
		private set => SetPropertyField(value, ref m_allGenerators);
	}

	public void Roll()
	{
		foreach (var generator in Generators)
			generator.Roll();
	}

	public void Dispose()
	{
		AppModel.Instance.Settings.SettingChanged -= OnSettingChanged;
	}

	private IReadOnlyList<ValueGeneratorViewModelBase> CreateGenerators()
	{
		var shouldRollManually = AppModel.Instance.Settings.Get<bool>(SettingsKeys.RollValueManually);
		var allGenerators = new List<ValueGeneratorViewModelBase>();
		m_randomSourcesAndGenerators.Clear();
		foreach (var randomSource in m_randomSources)
		{
			var generators = randomSource.Configurations
				.Select(x => ValueGeneratorViewModelBase.Create(randomSource.Kind, x, shouldRollManually, OnValueGenerated))
				.AsReadOnlyList();
			m_randomSourcesAndGenerators.Add((randomSource, generators));
			allGenerators.AddRange(generators);
		}

		return allGenerators;
	}

	private void OnValueGenerated()
	{
		if (m_allGenerators.Any(x => x.GeneratedValue is null))
			return;

		var allValues = m_randomSourcesAndGenerators
			.Select(randomSourceAndGenerators =>
			{
				var values = randomSourceAndGenerators.Generators.Select(x => x.GeneratedValue!.Value).AsReadOnlyList();
				RandomValueBase value = randomSourceAndGenerators.RandomSource switch
				{
					DiceSumSource dieSource => new DieValue(values.Sum()),
					DiceSequenceSource dieSource => new DieValue(values),
					CardSequenceSource cardSource => new CardValue(values),
					FixedSource fixedSource => new FixedValue(values.Count),
					_ => throw new NotImplementedException(),
				};
				return value;
			})
			.AsReadOnlyList();

		m_onValueGenerated(allValues);

		foreach (var generator in m_allGenerators)
			generator.OnReportingFinished();
	}

	private void OnSettingChanged(object? sender, GenericEventArgs<string> e)
	{
		if (e.Value == SettingsKeys.RollValueManually)
			Generators = CreateGenerators();
	}

	private readonly Action<IReadOnlyList<RandomValueBase>> m_onValueGenerated;
	private readonly IReadOnlyList<RandomSourceBase> m_randomSources;
	private readonly List<(RandomSourceBase RandomSource, IReadOnlyList<ValueGeneratorViewModelBase> Generators)> m_randomSourcesAndGenerators;
	private IReadOnlyList<ValueGeneratorViewModelBase> m_allGenerators;
}
