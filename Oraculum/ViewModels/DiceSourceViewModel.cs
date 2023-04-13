using System;
using System.Collections.Generic;
using System.Linq;
using Oraculum.Engine;

namespace Oraculum.ViewModels
{
	public class DiceViewModel : ViewModelBase
  {
    public DiceViewModel(int maxValue, Action<object?> onValueDisplayed)
    {
      MaxValue = maxValue;
      m_onValueDisplayed = onValueDisplayed ?? throw new ArgumentNullException(nameof(onValueDisplayed));

      ShouldAnimate = false;
      Value = maxValue;
      ShouldAnimate = true;
    }

    public int MaxValue { get; }

		public int Value
    {
			get => VerifyAccess(m_value);
			private set => SetPropertyField(value, ref m_value);
		}

		public bool ShouldAnimate
    {
      get => VerifyAccess(m_shouldAnimate);
      private set => SetPropertyField(value, ref m_shouldAnimate);
    }

    public void OnFinalValueDisplayed()
    {
      var key = m_lastKey;
      m_lastKey = null;
      m_onValueDisplayed(key);
    }

    public void SetValue(int value, object key)
    {
      m_lastKey = key;
      Value = 0;
      Value = value;
    }

		private readonly Action<object?> m_onValueDisplayed;

		private bool m_shouldAnimate;
    private int m_value;
    private object? m_lastKey;
	}

	public class DiceSourceViewModel : ViewModelBase
  {
    public DiceSourceViewModel(DiceSource source, Action<object?> onKeyGenerated)
    {
      m_diceSource = source;
      m_onKeyGenerated = onKeyGenerated;

      Dice = m_diceSource.Dice.Select(x => new DiceViewModel(x, OnValueDisplayed)).ToArray();
    }

		private void OnValueDisplayed(object? key)
		{
			if (key == m_lastKey)
      {
        m_valueDisplayedCount++;
        if (m_valueDisplayedCount == Dice.Count)
        {
          m_onKeyGenerated(m_lastKey);
          m_lastKey = null;
        }
      }
		}

		public IReadOnlyList<DiceViewModel> Dice { get; }

    public void Roll()
    {
      m_valueDisplayedCount = 0;
      var (key, values) = m_diceSource.GenerateResult();
      m_lastKey = key;
      for (int i = 0; i < values.Count; i++)
      {
        Dice[i].SetValue((int) values[i], key);
      }
    }

    private readonly DiceSource m_diceSource;
    private readonly Action<object?> m_onKeyGenerated;

    private int m_valueDisplayedCount;
    private object? m_lastKey;
  }
}
