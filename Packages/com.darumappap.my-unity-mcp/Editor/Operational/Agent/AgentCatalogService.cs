using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityAgentMcp
{
    internal static class AgentCatalogService
    {
        internal const int SUPPORTED_SCHEMA_VERSION = 5;

        private static readonly HashSet<string> EFFECTS = new HashSet<string>(StringComparer.Ordinal)
        {
            "none",
            "scene_mutation",
            "asset_mutation",
            "save",
            "bake",
            "capture_control",
        };

        private static readonly HashSet<string> REVISION_POLICIES = new HashSet<string>(StringComparer.Ordinal)
        {
            "must_remain",
            "may_advance",
        };

        internal static bool TryLoad(
            string path,
            IEnumerable<string> registeredDelegates,
            out AgentCatalogSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;

            try
            {
                return TryParse(File.ReadAllText(path), registeredDelegates, out snapshot, out error);
            }
            catch (Exception exception)
            {
                error = "Catalog read failed: " + exception.Message;
                return false;
            }
        }

        internal static bool TryParse(
            string json,
            IEnumerable<string> registeredDelegates,
            out AgentCatalogSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;

            if (registeredDelegates == null)
            {
                error = "Registered delegate names are required.";
                return false;
            }
            HashSet<string> registeredDelegateNames = new HashSet<string>(registeredDelegates, StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Catalog JSON is empty.";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception exception)
            {
                error = "Catalog JSON is invalid: " + exception.Message;
                return false;
            }

            if (root["schemaVersion"]?.Type != JTokenType.Integer ||
                root["schemaVersion"].Value<int>() != SUPPORTED_SCHEMA_VERSION)
            {
                error = "Catalog schemaVersion must be " + SUPPORTED_SCHEMA_VERSION + ".";
                return false;
            }

            if (!(root["domains"] is JArray domainsToken))
            {
                error = "Catalog domains must be an array.";
                return false;
            }

            if (!(root["creators"] is JArray creatorsToken))
            {
                error = "Catalog creators must be an array.";
                return false;
            }

            foreach (JToken domainToken in domainsToken)
            {
                if (!(domainToken is JObject domainObject))
                {
                    error = "Catalog domain entries must be objects.";
                    return false;
                }

                if (domainObject.Property("toolGroups") != null)
                {
                    error = "Catalog v5 domains must not contain toolGroups.";
                    return false;
                }

                if (!(domainObject["tools"] is JArray domainTools))
                {
                    error = "Catalog domain tools must be an array.";
                    return false;
                }

                foreach (JToken domainToolToken in domainTools)
                {
                    if (!(domainToolToken is JObject domainToolObject) ||
                        !(domainToolObject["policy"] is JObject policyObject))
                    {
                        error = "Catalog tool policy is required.";
                        return false;
                    }

                    string[] policyFields = { "effect", "approvalRequired", "approvalGroup", "revisionPolicy", "retryPolicy" };
                    foreach (string policyField in policyFields)
                    {
                        if (policyObject.Property(policyField) == null)
                        {
                            error = "Catalog tool policy field is missing: " + policyField;
                            return false;
                        }
                    }

                    if (policyObject["approvalRequired"].Type != JTokenType.Boolean ||
                        (policyObject["approvalGroup"].Type != JTokenType.Null && policyObject["approvalGroup"].Type != JTokenType.String))
                    {
                        error = "Catalog tool policy field types are invalid.";
                        return false;
                    }
                }
            }

            foreach (JToken creatorToken in creatorsToken)
            {
                if (!(creatorToken is JObject creatorObject))
                {
                    error = "Catalog creator entries must be objects.";
                    return false;
                }

                JToken creatorTools = creatorObject["tools"];
                if (!(creatorTools is JArray))
                {
                    error = "Catalog creator tools must remain a string array.";
                    return false;
                }

                foreach (JToken creatorTool in creatorTools)
                {
                    if (creatorTool.Type != JTokenType.String || string.IsNullOrWhiteSpace(creatorTool.Value<string>()))
                    {
                        error = "Catalog creator tools must contain only non-empty strings.";
                        return false;
                    }
                }

                if (creatorObject["creatorId"]?.Type != JTokenType.String ||
                    creatorObject["status"]?.Type != JTokenType.String ||
                    creatorObject["directUnityMutationAllowed"]?.Type != JTokenType.Boolean ||
                    creatorObject.Value<bool?>("directUnityMutationAllowed") != false)
                {
                    error = "Catalog creator identity/status/direct mutation contract is invalid.";
                    return false;
                }
            }

            AgentCatalogData data;
            try
            {
                data = JsonConvert.DeserializeObject<AgentCatalogData>(root.ToString(Formatting.None));
            }
            catch (Exception exception)
            {
                error = "Catalog DTO deserialization failed: " + exception.Message;
                return false;
            }

            if (data == null || data.domains == null || data.domains.Length == 0)
            {
                error = "Catalog must contain at least one domain.";
                return false;
            }

            if (data.creators == null || data.creators.Length == 0)
            {
                error = "Catalog must contain at least one creator.";
                return false;
            }

            var domains = new Dictionary<string, AgentCatalogDomainDefinition>(StringComparer.Ordinal);
            var toolIndex = new Dictionary<string, AgentCatalogToolDefinition>(StringComparer.Ordinal);

            for (int domainIndex = 0; domainIndex < data.domains.Length; domainIndex++)
            {
                AgentCatalogDomainDefinition domain = data.domains[domainIndex];
                if (domain == null || string.IsNullOrWhiteSpace(domain.domainId))
                {
                    error = "Catalog domainId is required.";
                    return false;
                }

                if (domains.ContainsKey(domain.domainId))
                {
                    error = "Catalog contains duplicate domainId: " + domain.domainId;
                    return false;
                }

                domains.Add(domain.domainId, domain);

                if (string.IsNullOrWhiteSpace(domain.status))
                {
                    error = "Catalog domain status is required: " + domain.domainId;
                    return false;
                }

                if (domain.directUnityMutationAllowed)
                {
                    error = "Catalog domains must not allow direct Unity mutation: " + domain.domainId;
                    return false;
                }

                if (domain.tools == null || domain.tools.Length == 0)
                {
                    error = "Catalog domain tools are required: " + domain.domainId;
                    return false;
                }

                foreach (AgentCatalogToolDefinition tool in domain.tools)
                {
                    if (tool == null || string.IsNullOrWhiteSpace(tool.name) || string.IsNullOrWhiteSpace(tool.group))
                    {
                        error = "Catalog tool name and group are required: " + domain.domainId;
                        return false;
                    }

                    if (tool.policy == null)
                    {
                        error = "Catalog tool policy is required: " + tool.name;
                        return false;
                    }

                    if (!EFFECTS.Contains(tool.policy.effect))
                    {
                        error = "Catalog tool effect is invalid: " + tool.name;
                        return false;
                    }

                    if (!REVISION_POLICIES.Contains(tool.policy.revisionPolicy))
                    {
                        error = "Catalog tool revisionPolicy is invalid: " + tool.name;
                        return false;
                    }

                    if (!string.Equals(tool.policy.retryPolicy, "none", StringComparison.Ordinal))
                    {
                        error = "Catalog tool retryPolicy must be none: " + tool.name;
                        return false;
                    }

                    if (tool.policy.approvalRequired)
                    {
                        if (!string.Equals(tool.policy.approvalGroup, tool.group, StringComparison.Ordinal) ||
                            !string.Equals(tool.policy.revisionPolicy, "may_advance", StringComparison.Ordinal))
                        {
                            error = "Catalog approved tool policy is inconsistent: " + tool.name;
                            return false;
                        }
                    }
                    else if (tool.policy.approvalGroup != null ||
                             !string.Equals(tool.policy.revisionPolicy, "must_remain", StringComparison.Ordinal))
                    {
                        error = "Catalog non-approved tool policy is inconsistent: " + tool.name;
                        return false;
                    }

                    if (!registeredDelegateNames.Contains(tool.name))
                    {
                        error = "Catalog tool has no registered delegate: " + tool.name;
                        return false;
                    }

                    if (toolIndex.ContainsKey(tool.name))
                    {
                        error = "Catalog contains duplicate tool name: " + tool.name;
                        return false;
                    }

                    toolIndex.Add(tool.name, tool);

                    tool.domainId = domain.domainId;
                }
            }

            var creators = new HashSet<string>(StringComparer.Ordinal);
            foreach (AgentCatalogCreatorDefinition creator in data.creators)
            {
                if (creator == null || string.IsNullOrWhiteSpace(creator.creatorId))
                {
                    error = "Catalog creatorId is required.";
                    return false;
                }

                if (!creators.Add(creator.creatorId))
                {
                    error = "Catalog contains duplicate creatorId: " + creator.creatorId;
                    return false;
                }

                if (creator.tools == null)
                {
                    error = "Catalog creator tools are required: " + creator.creatorId;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(creator.status))
                {
                    error = "Catalog creator status is required: " + creator.creatorId;
                    return false;
                }

                if (creator.directUnityMutationAllowed)
                {
                    error = "Catalog creators must not allow direct Unity mutation: " + creator.creatorId;
                    return false;
                }
            }

            snapshot = new AgentCatalogSnapshot(data, domains, toolIndex);
            return true;
        }
    }
}
