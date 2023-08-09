using System;
using System.Collections.Generic;
using System.Linq;

namespace Oraculum.ViewModels
{
	public static class TreeNodeUtility
	{
		public static IReadOnlyList<TreeNodeBase> GetAncesters(TreeNodeBase node)
		{
			var ancestry = new Queue<TreeNodeBase>();
			var currentNode = node;
			while (currentNode is not null)
			{
				ancestry.Enqueue(currentNode);
				currentNode = currentNode.Parent;
			}
			return ancestry.ToList();
		}

		public static IEnumerable<TreeNodeBase> EnumerateNodes(TreeNodeBase root, TreeNodeTraversalOrder order, bool onlyVisible, Func<TreeNodeBase, bool>? filterPredicate)
		{
			object nextNodes = order is TreeNodeTraversalOrder.BreadthFirst ? new Queue<TreeNodeBase>() :
				new Stack<TreeNodeBase>();
			AddNodeToScan(nextNodes, root);

			while (HasNodeToScan(nextNodes))
			{
				var node = GetNextNodeToScan(nextNodes);
				if (filterPredicate?.Invoke(node) ?? true)
				{
					yield return node;
					if (node is TreeBranch branch)
					{
						var children = onlyVisible ? branch.Children.OfType<TreeNodeBase>() : branch.GetUnfilteredChildren();
						foreach (var child in children)
							AddNodeToScan(nextNodes, child);
					}
				}
			}
		}

		private static void AddNodeToScan<T>(T nodesToScan, TreeNodeBase node)
			where T : notnull
		{
			if (nodesToScan is Queue<TreeNodeBase> queue)
				queue.Enqueue(node);
			else if (nodesToScan is Stack<TreeNodeBase> stack)
				stack.Push(node);
			else
				throw new ArgumentException($"Unhandled list type \"{nodesToScan.GetType().FullName}\".");
		}

		private static TreeNodeBase GetNextNodeToScan<T>(T nodesToScan)
			where T : notnull
		{
			if (nodesToScan is Queue<TreeNodeBase> queue)
				return queue.Dequeue();
			else if (nodesToScan is Stack<TreeNodeBase> stack)
				return stack.Pop();
			else
				throw new ArgumentException($"Unhandled list type \"{nodesToScan.GetType().FullName}\".");
		}

		private static bool HasNodeToScan<T>(T nodesToScan)
			where T : notnull
		{
			if (nodesToScan is Queue<TreeNodeBase> queue)
				return queue.Count != 0;
			else if (nodesToScan is Stack<TreeNodeBase> stack)
				return stack.Count != 0;
			else
				throw new ArgumentException($"Unhandled list type \"{nodesToScan.GetType().FullName}\".");
		}
	}
}
