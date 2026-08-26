using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityAgentMcp
{
    [Serializable]
    internal sealed class AgentCatalogData
    {
        public int schemaVersion;
        public AgentCatalogDomainDefinition[] domains;
        public AgentCatalogCreatorDefinition[] creators;
    }

    [Serializable]
    internal sealed class AgentCatalogDomainDefinition
    {
        public string domainId;
        public string status;
        public AgentCatalogToolDefinition[] tools;
        public bool directUnityMutationAllowed;
    }

    [Serializable]
    internal sealed class AgentCatalogToolDefinition
    {
        public string name;
        public string group;
        public AgentCatalogToolPolicy policy;

        internal string domainId;
    }

    [Serializable]
    internal sealed class AgentCatalogToolPolicy
    {
        public string effect;
        public bool approvalRequired;
        public string approvalGroup;
        public string revisionPolicy;
        public string retryPolicy;
    }

    [Serializable]
    internal sealed class AgentCatalogCreatorDefinition
    {
        public string creatorId;
        public string status;
        public string[] tools;
    }

    internal sealed class AgentCatalogSnapshot
    {
        internal AgentCatalogSnapshot(
            AgentCatalogData data,
            Dictionary<string, AgentCatalogDomainDefinition> domains,
            Dictionary<string, AgentCatalogToolDefinition> toolIndex)
        {
            Data = data;
            Domains = domains;
            ToolIndex = toolIndex;
        }

        internal AgentCatalogData Data { get; }

        internal Dictionary<string, AgentCatalogDomainDefinition> Domains { get; }

        internal Dictionary<string, AgentCatalogToolDefinition> ToolIndex { get; }

        internal bool TryGetDomain(string domainId, out AgentCatalogDomainDefinition domain)
        {
            return Domains.TryGetValue(domainId ?? string.Empty, out domain);
        }

        internal bool TryGetTool(string toolName, out AgentCatalogToolDefinition tool)
        {
            return ToolIndex.TryGetValue(toolName ?? string.Empty, out tool);
        }

        internal string[] GetCanonicalGroups(AgentCatalogDomainDefinition domain)
        {
            if (domain == null || domain.tools == null)
            {
                return Array.Empty<string>();
            }

            return domain.tools
                .Select(tool => tool.group)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        internal UnityAgentMcpDomainData[] BuildPublicDomains()
        {
            if (Data.domains == null)
            {
                return Array.Empty<UnityAgentMcpDomainData>();
            }

            return Data.domains
                .Select(domain => new UnityAgentMcpDomainData
                {
                    domainId = domain.domainId,
                    status = domain.status,
                    toolGroups = GetCanonicalGroups(domain),
                    tools = domain.tools.Select(tool => tool.name).ToArray(),
                    directUnityMutationAllowed = domain.directUnityMutationAllowed,
                })
                .ToArray();
        }
    }
}
