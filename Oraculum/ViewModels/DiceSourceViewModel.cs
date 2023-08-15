using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoldenAnvil.Utility.Windows.Async;
using Oraculum.Data;
using Oraculum.Engine;

namespace Oraculum.ViewModels
{
	public class DiceSourceViewModel : ViewModelBase
  {
    public DiceSourceViewModel(DiceSource source, Action onRollStarted, Func<TaskStateController, object, Task> onKeyGenerated)
    {
      m_diceSource = source;
      m_onRollStarted = onRollStarted;
      m_onKeyGenerated = onKeyGenerated;
      m_useManualDice = AppModel.Instance.Settings.Get<bool?>(SettingsKeys.UseManualDice) ?? c_useManualDiceDefault;

      Dice = m_diceSource.Dice.Select(x => new DiceViewModel(x, OnValueDisplayedAsync)).ToArray();
    }

    public bool UseManualDice
		{
			get => VerifyAccess(m_useManualDice);
			set
			{
				if (SetPropertyField(value, ref m_useManualDice))
					AppModel.Instance.Settings.Set(SettingsKeys.UseManualDice, value);
			}
		}

		private async Task OnValueDisplayedAsync(TaskStateController state, object key)
		{
			if (key == m_lastKey)
      {
        m_valueDisplayedCount++;
        if (m_valueDisplayedCount == Dice.Count)
        {
          await m_onKeyGenerated(state, m_lastKey).ConfigureAwait(false);
          m_lastKey = null;
        }
      }
		}

		public IReadOnlyList<DiceViewModel> Dice { get; }

    public void Roll()
    {
      m_onRollStarted();
      m_valueDisplayedCount = 0;
      var (key, values) = m_diceSource.GenerateResult();
      m_lastKey = key;
      for (int i = 0; i < values.Count; i++)
      {
        Dice[i].SetValue((int) values[i], key);
      }
    }

    private const bool c_useManualDiceDefault = false;

    private readonly DiceSource m_diceSource;
    private readonly Action m_onRollStarted;
    private readonly Func<TaskStateController, object, Task> m_onKeyGenerated;

    private int m_valueDisplayedCount;
    private object? m_lastKey;
    private bool m_useManualDice;
	}
}
