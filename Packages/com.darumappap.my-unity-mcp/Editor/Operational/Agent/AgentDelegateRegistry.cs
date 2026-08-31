#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityGraphicsMcp;

namespace UnityAgentMcp
{
	internal sealed class AgentDelegateRegistry
	{
		private readonly Dictionary<string, Func<JObject, object>> _delegates;

		private AgentDelegateRegistry(Dictionary<string, Func<JObject, object>> delegates)
		{
			_delegates = delegates;
		}

		internal IEnumerable<string> RegisteredNames => _delegates.Keys;

		internal static AgentDelegateRegistry Discover()
		{
			Dictionary<string, Func<JObject, object>> handlers =
				new Dictionary<string, Func<JObject, object>>(StringComparer.Ordinal);
			System.Reflection.Assembly assembly = typeof(InspectProjectTool).Assembly;
			foreach (Type type in assembly.GetTypes())
			{
				CustomAttributeData attribute = type.GetCustomAttributesData().FirstOrDefault(value =>
					string.Equals(
						value.AttributeType.FullName,
						"MCPForUnity.Editor.Tools.McpForUnityToolAttribute",
						StringComparison.Ordinal));
				if (attribute == null || attribute.ConstructorArguments.Count == 0)
				{
					continue;
				}
				string toolName = attribute.ConstructorArguments[0].Value as string;
				if (string.IsNullOrWhiteSpace(toolName))
				{
					continue;
				}
				MethodInfo handleCommand = type.GetMethod(
					"HandleCommand",
					BindingFlags.Public | BindingFlags.Static,
					null,
					new[] {typeof(JObject)},
					null);
				if (handleCommand == null)
				{
					continue;
				}
				if (handlers.ContainsKey(toolName))
				{
					throw new InvalidOperationException($"Duplicate MCP Tool delegate: {toolName}");
				}
				MethodInfo method = handleCommand;
				handlers.Add(toolName, value => method.Invoke(null, new object[] {value}));
			}
			return new AgentDelegateRegistry(handlers);
		}

		internal bool TryInvoke(string toolName, JObject parameters, out object result, out Exception exception)
		{
			result = null;
			exception = null;
			if (!_delegates.TryGetValue(toolName ?? string.Empty, out Func<JObject, object> handler))
			{
				return false;
			}
			try
			{
				result = handler(parameters ?? new JObject());
				return true;
			}
			catch (Exception caught)
			{
				exception = caught;
				return true;
			}
		}
	}
}

#endif
