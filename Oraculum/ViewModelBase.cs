using System.Windows.Threading;
using GoldenAnvil.Utility.Windows;

namespace Oraculum
{
	public abstract class ViewModelBase : NotifyPropertyChangedDispatcherBase
	{
		protected ViewModelBase()
		{
		}

		protected ViewModelBase(Dispatcher dispatcher)
			: base(dispatcher)
		{
		}
	}
}
