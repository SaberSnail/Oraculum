using System;
using System.Threading.Tasks;
using GoldenAnvil.Utility.Windows.Async;
using System.Globalization;

namespace Oraculum.ViewModels
{
	public class DiceViewModel : ViewModelBase
  {
    public DiceViewModel(int maxValue, Func<TaskStateController, object, Task> onValueDisplayed)
    {
      MaxValue = maxValue;
      ManualHintText = string.Format(CultureInfo.CurrentCulture, OurResources.SingleDieFormat, maxValue);
      m_onValueDisplayed = onValueDisplayed ?? throw new ArgumentNullException(nameof(onValueDisplayed));

      ShouldAnimate = false;
      Value = maxValue;
      ShouldAnimate = true;
    }

    public int MaxValue { get; }

		public int? ManualValue
		{
			get => VerifyAccess(m_manualValue);
			set
			{
				if (SetPropertyField(value, ref m_manualValue) && value is not null)
        {
          SetValue()
        }
			}
		}

		public string ManualHintText
		{
			get => VerifyAccess(m_manualHintText);
			set => SetPropertyField(value, ref m_manualHintText);
		}

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
      var key = m_lastKey!;
      m_lastKey = null;
			m_displayFinalValueWork?.Cancel();
			m_displayFinalValueWork = TaskWatcher.Execute(async state =>
			{
				await m_onValueDisplayed(state, key).ConfigureAwait(false);
			}, AppModel.Instance.TaskGroup);
    }

    public void SetValue(int value, object key)
    {
      m_lastKey = key;
      Value = 0;
      Value = value;
    }

		private readonly Func<TaskStateController, object, Task> m_onValueDisplayed;

		private bool m_shouldAnimate;
    private int m_value;
    private object? m_lastKey;
    private TaskWatcher? m_displayFinalValueWork;
    private int? m_manualValue;
    private string m_manualHintText;
	}
}
