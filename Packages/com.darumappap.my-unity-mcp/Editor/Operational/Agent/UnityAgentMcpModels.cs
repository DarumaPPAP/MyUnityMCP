#if UNITY_EDITOR

using Newtonsoft.Json.Linq;

namespace UnityAgentMcp
{
	public enum E_AGENT_EXECUTION_STATUS
	{
		PREVIEW,
		AWAITING_APPROVAL,
		RUNNING,
		SUCCEEDED,
		PARTIAL,
		FAILED,
		CANCELLED,
		INTERRUPTED
	}

	public sealed class UnityAgentMcpStepInput
	{
		public string stepId;
		public string domainId;
		public string toolName;
		public string toolGroup;
		public string[] dependsOn;
		public JObject parameters;
	}

	public sealed class UnityAgentMcpCatalogData
	{
		public int schemaVersion;
		public UnityAgentMcpDomainData[] domains;
	}

	public sealed class UnityAgentMcpDomainData
	{
		public string domainId;
		public string status;
		public string[] toolGroups;
		public string[] tools;
		public bool directUnityMutationAllowed;
	}
}

#endif
