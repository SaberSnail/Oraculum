using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using GoldenAnvil.Utility;

namespace Oraculum.Data
{
	
	public sealed class SettingsManager
	{
		public SettingsManager()
		{
			m_preferences = new Dictionary<string, string>();
			m_dispatcher = Dispatcher.CurrentDispatcher;
		}

		public event EventHandler<GenericEventArgs<string>>? SettingChanged;

		public async Task InitializeAsync(CancellationToken cancellationToken)
		{
			var data = AppModel.Instance.Data;
			var keyValues = await data.GetAllSettingsAsync(cancellationToken).ConfigureAwait(false);
			foreach (var keyValue in keyValues)
				m_preferences.Add(keyValue.Key, keyValue.Value);
		}

		public T? Get<T>(string key)
		{
			VerifyAccess();
			if (!m_preferences.TryGetValue(key, out var value))
				return default(T);
			return ConversionUtility.Convert<T>(value);
		}

		public void Set<T>(string key, T? value)
		{
			if (value is null)
				Clear(key);

			VerifyAccess();
			var newValue = ConversionUtility.Convert<string>(value);
			if (!m_preferences.TryGetValue(key, out var oldValue) || oldValue != newValue)
			{
				m_preferences[key] = newValue;
				AppModel.Instance.Data.SetSetting(key, newValue);
				SettingChanged.Raise(this, new GenericEventArgs<string>(key));
			}
		}

		public void Clear(string key)
		{
			VerifyAccess();
			if (m_preferences.Remove(key))
				AppModel.Instance.Data.DeleteSetting(key);
		}

		private void VerifyAccess()
		{
			if (!m_dispatcher.CheckAccess())
				throw new InvalidOperationException("This code must be called on the same dispatcher as the object was created.");
		}

		private readonly Dictionary<string, string> m_preferences;
		private readonly Dispatcher m_dispatcher;
	}
}
