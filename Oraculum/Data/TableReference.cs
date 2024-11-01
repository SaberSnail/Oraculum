using System;
using System.Windows.Threading;

namespace Oraculum.Data
{
	public abstract class TableReference : ViewModelBase, IEquatable<TableReference>
	{
		public Guid Id { get; }
		public string Title
		{
			get => VerifyAccess(m_title);
			set
			{
				if (SetPropertyField(value, ref m_title))
					OnTitleChanged();
			}
		}

		protected TableReference(Guid id, string title, Dispatcher dispatcher)
			: base(dispatcher)
		{
			Id = id;
			m_title = title;
		}

		public override bool Equals(object? that) => Equals(that as TableReference);

		public bool Equals(TableReference? that) =>
			that is { } && that.GetType() == GetType() && Id == that.Id;

		public override int GetHashCode() => Id.GetHashCode();

		public static bool operator ==(TableReference left, TableReference? right) =>
			left.Equals(right);

		public static bool operator !=(TableReference left, TableReference? right) =>
			!left.Equals(right);

		public override string ToString() => m_title;

		protected abstract void OnTitleChanged();

		private string m_title;
	}
}
