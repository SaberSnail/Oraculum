using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Windows;
using GoldenAnvil.Utility.Windows.Async;

namespace Oraculum.Data
{
	public sealed class SettingsManager : NotifyPropertyChangedDispatcherBase
	{
		public SettingsManager()
		{
			m_preferences = new Dictionary<string, string>();
			m_taskGroup = new TaskGroup();
			m_writeTasks = new List<TaskWatcher>();
		}

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
			var localKey = key;
			var newValue = ConversionUtility.Convert<string>(value);
			if (!m_preferences.TryGetValue(localKey, out var oldValue) || oldValue != newValue)
			{
				m_preferences[localKey] = newValue;
				DoWork(async state =>
				{
					var data = AppModel.Instance.Data;
					await data.SetSettingAsync(localKey, newValue, state.CancellationToken).ConfigureAwait(false);
				});
			}
		}

		public void Clear(string key)
		{
			VerifyAccess();
			var localKey = key;
			if (m_preferences.Remove(localKey))
			{
				DoWork(async state =>
				{
					var data = AppModel.Instance.Data;
					await data.DeleteSettingAsync(localKey, state.CancellationToken).ConfigureAwait(false);
				});
			}
		}

		private void DoWork(Func<TaskStateController, Task> work)
		{
			var watcher = TaskWatcher.Create(work, m_taskGroup);
			m_writeTasks.Add(watcher);
			watcher.PropertyChanged += OnWorkCompleted;
			watcher.Start();

			void OnWorkCompleted(object? sender, PropertyChangedEventArgs e)
			{
				if (e.PropertyName == nameof(TaskWatcher.IsCompleted))
				{
					m_writeTasks.Remove(watcher);
					watcher.PropertyChanged -= OnWorkCompleted;
				}
			}
		}

		public async Task WaitForWriteAsync(CancellationToken cancellationToken)
		{
			var tasks = m_writeTasks.Select(x => x.Task).ToArray();
			await Task.WhenAll(tasks).ConfigureAwait(false);
		}

		private readonly Dictionary<string, string> m_preferences;
		private readonly List<TaskWatcher> m_writeTasks;

		private TaskGroup m_taskGroup;
	}
}
