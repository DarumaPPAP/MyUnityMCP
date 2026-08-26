#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityAgentMcp;
using UnityGraphicsMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityAgentMcpRuntimeTests
	{
		[SetUp]
		public void SetUp()
		{
			AgentExecutionHistoryStore.StorageRootOverrideForTests = null;
			AgentExecutionTraceStore.StorageRootOverrideForTests = null;
			UnityAgentMcpRuntime.Instance.ResetExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ResetPersistenceForTests();
		}

		[TearDown]
		public void TearDown()
		{
			UnityAgentMcpRuntime.Instance.ResetExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ResetPersistenceForTests();
			AgentExecutionHistoryStore.StorageRootOverrideForTests = null;
			AgentExecutionTraceStore.StorageRootOverrideForTests = null;
		}

		private static UnityAgentMcpStepInput GraphicsInspectStep(string stepId = "inspect", params string[] dependsOn)
		{
			return new UnityAgentMcpStepInput
			{
				stepId = stepId,
				domainId = "unity_graphics_mcp",
				toolName = "graphics.inspect_project",
				toolGroup = "inspect",
				dependsOn = dependsOn ?? Array.Empty<string>(),
				parameters = new JObject()
			};
		}

		private static UnityAgentMcpStepInput GraphicsMutationStep(string stepId = "mutate", params string[] dependsOn)
		{
			return new UnityAgentMcpStepInput
			{
				stepId = stepId,
				domainId = "unity_graphics_mcp",
				toolName = "graphics.apply_plan",
				toolGroup = "mutate",
				dependsOn = dependsOn ?? Array.Empty<string>(),
				parameters = new JObject()
			};
		}

		private static UnityAgentMcpStepInput ProfilerInspectStep(string stepId = "profiler")
		{
			return new UnityAgentMcpStepInput
			{
				stepId = stepId,
				domainId = "unity_profiler_mcp",
				toolName = "profiler.inspect_environment",
				toolGroup = "profiler",
				dependsOn = Array.Empty<string>(),
				parameters = new JObject()
			};
		}

		private static string CatalogJson()
		{
			return File.ReadAllText("Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpCatalog.json");
		}

		private static bool TryParseCatalog(JObject root, out AgentCatalogSnapshot snapshot, out string error)
		{
			return AgentCatalogService.TryParse(
				root.ToString(),
				UnityAgentMcpRuntime.RegisteredDomainDelegateNamesForTests,
				out snapshot,
				out error);
		}

		[Test]
		public void InspectCapabilities_LoadsCatalogAndKeepsDirectMutationDisabled()
		{
			JObject result = UnityAgentMcpRuntime.Instance.InspectCapabilities();

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("directUnityMutation"), Is.False);
			Assert.That(result.Value<bool>("cooperativeExecution"), Is.True);
			Assert.That(result.Value<bool>("integrationCandidateExecutionEnabled"), Is.True);
			Assert.That(result.Value<int>("defaultExecutionTimeoutSeconds"), Is.GreaterThan(0));
			Assert.That(result["domains"]?.Any(), Is.True);
		}

		[Test]
		public void CatalogV5_ProjectsLegacyInspectCapabilitiesShapeForAllDomains()
		{
			JObject result = UnityAgentMcpRuntime.Instance.InspectCapabilities();
			JArray domains = (JArray)result["domains"];
			JArray catalogDomains = (JArray)JObject.Parse(CatalogJson())["domains"];
			string[] expectedDomainIds =
			{
				"unity_graphics_mcp", "unity_profiler_mcp", "unity_addressables_mcp", "unity_ui_mcp",
				"unity_animation_mcp", "unity_audio_mcp", "unity_cinematic_mcp"
			};
			string[][] expectedGroups =
			{
				new[] {"inspect", "plan", "mutate", "save", "bake", "capture", "evaluate_and_refine", "execution"},
				new[] {"profiler"},
				new[] {"addressables"},
				new[] {"ui"},
				new[] {"animation"},
				new[] {"audio"},
				new[] {"cinematic"}
			};
			string[] expectedFields = {"domainId", "status", "toolGroups", "tools", "directUnityMutationAllowed"};

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(domains.Count, Is.EqualTo(7));
			for (int index = 0; index < domains.Count; index++)
			{
				JObject domain = (JObject)domains[index];
				JObject catalogDomain = (JObject)catalogDomains[index];
				Assert.That(domain.Properties().Select(value => value.Name).ToArray(), Is.EqualTo(expectedFields));
				Assert.That(domain["domainId"]?.Type, Is.EqualTo(JTokenType.String));
				Assert.That(domain.Value<string>("domainId"), Is.EqualTo(expectedDomainIds[index]));
				Assert.That(domain["status"]?.Type, Is.EqualTo(JTokenType.String));
				Assert.That(domain.Value<string>("status"), Is.EqualTo(catalogDomain.Value<string>("status")));
				Assert.That(domain["toolGroups"]?.Type, Is.EqualTo(JTokenType.Array));
				Assert.That(domain["toolGroups"].All(value => value.Type == JTokenType.String), Is.True);
				Assert.That(domain["toolGroups"].Values<string>().ToArray(), Is.EqualTo(expectedGroups[index]));
				Assert.That(domain["tools"]?.Type, Is.EqualTo(JTokenType.Array));
				Assert.That(domain["tools"].All(value => value.Type == JTokenType.String), Is.True);
				Assert.That(domain["tools"].Values<string>().ToArray(), Is.EqualTo(catalogDomain["tools"].Values<JObject>().Select(value => value.Value<string>("name")).ToArray()));
				Assert.That(domain["directUnityMutationAllowed"]?.Type, Is.EqualTo(JTokenType.Boolean));
				Assert.That(domain.Value<bool>("directUnityMutationAllowed"), Is.False);
			}
			Assert.That(result.ToString(), Does.Not.Contain("policy"));
		}

		[Test]
		public void CatalogV5_PreservesApprovalSet()
		{
			Assert.That(TryParseCatalog(JObject.Parse(CatalogJson()), out AgentCatalogSnapshot snapshot, out string error), Is.True, error);
			string[] expected =
			{
				"graphics.apply_plan", "graphics.undo_last_transaction", "graphics.apply_environment_plan",
				"graphics.undo_last_environment_transaction", "graphics.apply_save_plan", "graphics.bake_dependencies",
				"graphics.start_apv_bake", "graphics.get_apv_bake_status", "graphics.cancel_apv_bake",
				"addressables.apply_entry", "ui.apply_rect_transform", "animation.apply_parameter",
				"audio.apply_source", "cinematic.apply_director"
			};

			Assert.That(snapshot.ToolIndex.Values.Where(value => value.policy.approvalRequired).Select(value => value.name).ToArray(), Is.EqualTo(expected));
		}

		[Test]
		public void CatalogV5_RejectsDuplicateTool()
		{
			JObject root = JObject.Parse(CatalogJson());
			JArray tools = (JArray)root["domains"][0]["tools"];
			tools.Add(tools[0].DeepClone());

			Assert.That(TryParseCatalog(root, out _, out string error), Is.False);
			Assert.That(error, Does.Contain("duplicate tool"));
		}

		[Test]
		public void CatalogV5_RejectsUnknownAndMissingRegisteredTools()
		{
			JObject unknownTool = JObject.Parse(CatalogJson());
			((JObject)unknownTool["domains"][0]["tools"][0])["name"] = "graphics.unknown";
			Assert.That(TryParseCatalog(unknownTool, out _, out string unknownError), Is.False);
			Assert.That(unknownError, Does.Contain("registered delegate"));
		}

		[Test]
		public void CatalogV5_RejectsMissingPolicy()
		{
			JObject root = JObject.Parse(CatalogJson());
			((JObject)root["domains"][0]["tools"][0]).Remove("policy");

			Assert.That(TryParseCatalog(root, out _, out string error), Is.False);
			Assert.That(error, Does.Contain("policy"));
		}

		[Test]
		public void CatalogV5_RejectsInvalidEffectAndRetryPolicy()
		{
			JObject invalidEffect = JObject.Parse(CatalogJson());
			((JObject)invalidEffect["domains"][0]["tools"][0]["policy"])["effect"] = "execution_control";
			Assert.That(TryParseCatalog(invalidEffect, out _, out string effectError), Is.False);
			Assert.That(effectError, Does.Contain("effect"));

			JObject invalidRetry = JObject.Parse(CatalogJson());
			((JObject)invalidRetry["domains"][0]["tools"][0]["policy"])["retryPolicy"] = "safe_retry";
			Assert.That(TryParseCatalog(invalidRetry, out _, out string retryError), Is.False);
			Assert.That(retryError, Does.Contain("retryPolicy"));
		}

		[Test]
		public void CatalogV5_RejectsLegacyDomainToolGroupsAndCreatorObjects()
		{
			JObject legacyDomain = JObject.Parse(CatalogJson());
			legacyDomain["domains"][0]["toolGroups"] = new JArray("inspect");
			Assert.That(TryParseCatalog(legacyDomain, out _, out string domainError), Is.False);
			Assert.That(domainError, Does.Contain("toolGroups"));

			JObject creatorObjects = JObject.Parse(CatalogJson());
			creatorObjects["creators"][0]["tools"][0] = new JObject { ["name"] = "world.compile_workflow" };
			Assert.That(TryParseCatalog(creatorObjects, out _, out string creatorError), Is.False);
			Assert.That(creatorError, Does.Contain("creator tools"));
		}

		[Test]
		public void CatalogV5_RejectsCreatorDirectMutation()
		{
			JObject creatorMutation = JObject.Parse(CatalogJson());
			creatorMutation["creators"][0]["directUnityMutationAllowed"] = true;

			Assert.That(TryParseCatalog(creatorMutation, out _, out string error), Is.False);
			Assert.That(error, Does.Contain("creator"));
		}

		[Test]
		public void CatalogV5_CreatorContractRemainsStringArrayAndOrdered()
		{
			JArray creators = (JArray)JObject.Parse(CatalogJson())["creators"];
			string[] expectedIds = {"world_creator", "movie_creator", "live_creator"};
			string[][] expectedTools =
			{
				new[] {"world.compile_workflow", "world.start_preflight", "world.create_review_handoff"},
				new[] {"movie.compile_production", "movie.preview_production", "movie.create_review_handoff"},
				new[] {"live.compile_show", "live.preview_show", "live.create_operator_handoff"}
			};

			Assert.That(creators.Count, Is.EqualTo(3));
			for (int index = 0; index < creators.Count; index++)
			{
				JObject creator = (JObject)creators[index];
				Assert.That(creator.Properties().Select(value => value.Name).ToArray(), Is.EqualTo(new[]
					{"creatorId", "status", "tools", "directUnityMutationAllowed"}));
				Assert.That(creator.Value<string>("creatorId"), Is.EqualTo(expectedIds[index]));
				Assert.That(creator.Value<string>("status"), Is.Not.Empty);
				Assert.That(creator["tools"]?.Type, Is.EqualTo(JTokenType.Array));
				Assert.That(creator["tools"].All(value => value.Type == JTokenType.String), Is.True);
				Assert.That(creator["tools"].Values<string>().ToArray(), Is.EqualTo(expectedTools[index]));
				Assert.That(creator.Value<bool>("directUnityMutationAllowed"), Is.False);
			}
		}

		[Test]
		public void CatalogV5_FingerprintChangesWhenRawWhitespaceChanges()
		{
			byte[] original = Encoding.UTF8.GetBytes(CatalogJson());
			byte[] changed = Encoding.UTF8.GetBytes(CatalogJson() + "\n");

			Assert.That(AgentCatalogService.TryParse(
				original,
				UnityAgentMcpRuntime.RegisteredDomainDelegateNamesForTests,
				out AgentCatalogSnapshot originalSnapshot,
				out string originalError), Is.True, originalError);
			Assert.That(AgentCatalogService.TryParse(
				changed,
				UnityAgentMcpRuntime.RegisteredDomainDelegateNamesForTests,
				out AgentCatalogSnapshot changedSnapshot,
				out string changedError), Is.True, changedError);

			Assert.That(originalSnapshot.Fingerprint, Is.Not.EqualTo(changedSnapshot.Fingerprint));
			Assert.That(originalSnapshot.SchemaVersion, Is.EqualTo(5));
		}

		[Test]
		public void GraphBound_UsesNonNullStepsAndSharesValidatorForValidateAndCompile()
		{
			UnityAgentMcpStepInput[] sixtyFour = Enumerable.Range(0, 64)
				.Select(index => GraphicsInspectStep("step-" + index))
				.ToArray();
			UnityAgentMcpStepInput[] sixtyFive = Enumerable.Range(0, 65)
				.Select(index => GraphicsInspectStep("step-" + index))
				.ToArray();
			UnityAgentMcpStepInput[] sixtyFourAndNull = sixtyFour.Concat(new UnityAgentMcpStepInput[] {null}).ToArray();

			JObject validateSixtyFour = UnityAgentMcpRuntime.Instance.ValidateWorkflow(sixtyFour);
			JObject compileSixtyFour = UnityAgentMcpRuntime.Instance.CompileGraph(Session.Revision, sixtyFourAndNull);
			JObject validateSixtyFive = UnityAgentMcpRuntime.Instance.ValidateWorkflow(sixtyFive);
			JObject compileSixtyFive = UnityAgentMcpRuntime.Instance.CompileGraph(Session.Revision, sixtyFive);
			JObject validateEmpty = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new UnityAgentMcpStepInput[] {null, null});

			Assert.That(validateSixtyFour.Value<bool>("success"), Is.True, validateSixtyFour.ToString());
			Assert.That(validateSixtyFour.Value<int>("stepCount"), Is.EqualTo(64));
			Assert.That(compileSixtyFour.Value<bool>("success"), Is.True, compileSixtyFour.ToString());
			Assert.That(compileSixtyFour.Value<int>("stepCount"), Is.EqualTo(64));
			Assert.That(validateSixtyFive.Value<string>("errorCode"), Is.EqualTo("AGENT-GRAPH-TOO-LARGE"));
			Assert.That(compileSixtyFive.Value<string>("errorCode"), Is.EqualTo("AGENT-GRAPH-TOO-LARGE"));
			Assert.That(validateEmpty.Value<string>("errorCode"), Is.EqualTo("AGENT-WORKFLOW-EMPTY"));
		}

		[Test]
		public void CatalogFingerprintMismatch_FailsCompilePreviewAndStartClosed()
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			try
			{
				long revision = Session.Revision;
				JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
				Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());

				string changedPath = Path.Combine(temporaryDirectory, "catalog.json");
				File.WriteAllText(changedPath, CatalogJson() + "\n", new UTF8Encoding(false));
				UnityAgentMcpRuntime.CatalogPathOverrideForTests = changedPath;

				JObject compileAfterChange = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep("compile-after-change")});
				JObject previewAfterChange = UnityAgentMcpRuntime.Instance.PreviewExecution(compiled.Value<string>("graphId"));
				JObject startAfterChange = UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);

				Assert.That(compileAfterChange.Value<string>("errorCode"), Is.EqualTo("AGENT-CATALOG-CHANGED"));
				Assert.That(previewAfterChange.Value<string>("errorCode"), Is.EqualTo("AGENT-CATALOG-CHANGED"));
				Assert.That(startAfterChange.Value<string>("errorCode"), Is.EqualTo("AGENT-CATALOG-CHANGED"));
			}
			finally
			{
				UnityAgentMcpRuntime.CatalogPathOverrideForTests = null;
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		[Test]
		public void CatalogReadFailure_FailsBeforeDelegateExecution()
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			try
			{
				long revision = Session.Revision;
				JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
				string missingPath = Path.Combine(temporaryDirectory, "missing-catalog.json");
				UnityAgentMcpRuntime.CatalogPathOverrideForTests = missingPath;

				JObject result = UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);

				Assert.That(result.Value<bool>("success"), Is.False);
				Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-CATALOG-CHANGED"));
				Assert.That(result["executionId"], Is.Null);
			}
			finally
			{
				UnityAgentMcpRuntime.CatalogPathOverrideForTests = null;
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		[Test]
		public void ResultNormalizer_FailsClosedForUnknownAndContradictoryShapes()
		{
			Assert.That(AgentResultNormalizer.Normalize((JToken)null).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.AMBIGUOUS));
			Assert.That(AgentResultNormalizer.Normalize(JValue.CreateString("success")).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.AMBIGUOUS));
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["success"] = true }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.AMBIGUOUS));
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["status"] = "UNKNOWN", ["success"] = true }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.AMBIGUOUS));
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["status"] = "SUCCESS", ["success"] = false }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.AMBIGUOUS));
		}

		[Test]
		public void ResultNormalizer_RecognizesKnownSuccessFailurePartialUnsupportedAndEnvelopeShapes()
		{
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["status"] = "SUCCESS", ["success"] = true }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.SUCCEEDED));
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["status"] = "SUCCEEDED", ["success"] = true }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.SUCCEEDED));
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["status"] = "FAILED", ["success"] = false }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.FAILED));
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["status"] = "PARTIAL", ["success"] = true }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.PARTIAL));
			Assert.That(AgentResultNormalizer.Normalize(new JObject { ["status"] = "UNSUPPORTED", ["success"] = false }).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.UNSUPPORTED));

			AgentNormalizedResult domainFailure = AgentResultNormalizer.Normalize(new JObject
			{
				["status"] = "FAILED",
				["success"] = false,
				["errorCode"] = "TEST_FAILURE",
				["message"] = "failure",
				["data"] = new JObject { ["details"] = true }
			});
			Assert.That(domainFailure.Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.FAILED));
			Assert.That(domainFailure.ErrorCode, Is.EqualTo("TEST_FAILURE"));
			Assert.That(domainFailure.Message, Is.EqualTo("failure"));

			Assert.That(AgentResultNormalizer.Normalize(new JObject
			{
				["status"] = "SUCCESS",
				["success"] = true,
				["data"] = new JObject { ["actualPayload"] = true }
			}).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.SUCCEEDED));
			Assert.That(AgentResultNormalizer.Normalize(new JObject
			{
				["status"] = "PARTIAL",
				["success"] = true,
				["data"] = new JObject { ["partialPayload"] = true }
			}).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.PARTIAL));
			Assert.That(AgentResultNormalizer.Normalize(new JObject
			{
				["status"] = "UNSUPPORTED",
				["success"] = false,
				["data"] = new JObject { ["reason"] = "unsupported" }
			}).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.UNSUPPORTED));
			Assert.That(AgentResultNormalizer.Normalize(new JObject
			{
				["status"] = "SUCCESS",
				["success"] = true,
				["data"] = new JObject
				{
					["status"] = "UNKNOWN_PAYLOAD_STATUS",
					["success"] = false
				}
			}).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.SUCCEEDED));
			Assert.That(AgentResultNormalizer.Normalize(new JObject
			{
				["status"] = "SUCCESS",
				["success"] = false,
				["data"] = new JObject()
			}).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.AMBIGUOUS));

			Assert.That(AgentResultNormalizer.Normalize(new JObject
			{
				["success"] = true,
				["message"] = "success envelope",
				["data"] = new JObject { ["status"] = "SUCCESS", ["IsSuccessful"] = true }
			}).Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.SUCCEEDED));
			AgentNormalizedResult bridgeFailure = AgentResultNormalizer.Normalize(new JObject
			{
				["success"] = false,
				["code"] = "FAILED",
				["error"] = "failed",
				["data"] = new JObject { ["status"] = "FAILED", ["IsSuccessful"] = false }
			});
			Assert.That(bridgeFailure.Outcome, Is.EqualTo(E_AGENT_STEP_OUTCOME.FAILED));
			Assert.That(bridgeFailure.ErrorCode, Is.EqualTo("FAILED"));
			Assert.That(bridgeFailure.Message, Is.EqualTo("failed"));
		}

		[Test]
		public void HistoryProjection_RedactsRawExecutionData()
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			try
			{
				AgentExecutionHistoryStore.StorageRootOverrideForTests = temporaryDirectory;
				AgentExecutionTraceStore.StorageRootOverrideForTests = temporaryDirectory;
				UnityAgentMcpRuntime.Instance.ResetPersistenceForTests();
				long revision = Session.Revision;
				JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
				JObject started = UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);
				UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();

				JObject history = JObject.Parse(File.ReadLines(UnityAgentMcpRuntime.HistoryPathForTests).Single());
				HashSet<string> allowed = new HashSet<string>(new[]
				{
					"schemaVersion", "executionId", "graphId", "status", "startedAtUtc", "completedAtUtc",
					"timeoutSeconds", "completedStepCount", "totalStepCount", "expectedRevision", "errorCode", "stepSummaries"
				}, StringComparer.Ordinal);

				Assert.That(history.Properties().All(value => allowed.Contains(value.Name)), Is.True, history.ToString());
				Assert.That(history["stepSummaries"].Children<JObject>().All(summary =>
					summary.Properties().Select(value => value.Name).SequenceEqual(new[] {"stepId", "domainId", "toolName", "resultCode", "durationMs"})), Is.True);
				Assert.That(history.ToString(), Does.Not.Contain("delegatedResult"));
				Assert.That(history.ToString(), Does.Not.Contain("parameters"));
				Assert.That(history.ToString(), Does.Not.Contain("approvalToken"));
				Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"));
			}
			finally
			{
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		[Test]
		public void LegacyHistory_IsSanitizedAndAtomicallyRewritten()
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			try
			{
				string historyPath = Path.Combine(temporaryDirectory, "history.jsonl");
				File.WriteAllText(historyPath, new JObject
				{
					["executionId"] = "legacy-execution",
					["graphId"] = "legacy-graph",
					["status"] = "SUCCEEDED",
					["message"] = "C:\\secret\\input",
					["approvalToken"] = "token",
					["params"] = new JObject { ["password"] = "secret" },
					["delegatedResult"] = new JObject { ["data"] = "raw" },
					["stepResults"] = new JArray(new JObject { ["raw"] = "payload" }),
					["stepSummaries"] = new JArray(new JObject
					{
						["stepId"] = "step",
						["domainId"] = "domain",
						["toolName"] = "tool",
						["resultCode"] = "SUCCEEDED",
						["durationMs"] = 1.0,
						["nested"] = new JObject { ["secret"] = "value" }
					})
				}.ToString(Formatting.None) + Environment.NewLine);

				AgentExecutionHistoryStore.StorageRootOverrideForTests = temporaryDirectory;
				AgentExecutionHistoryStore store = new AgentExecutionHistoryStore();
				store.Load();
				JObject migrated = JObject.Parse(File.ReadLines(historyPath).Single());
				HashSet<string> allowed = new HashSet<string>(new[]
				{
					"schemaVersion", "executionId", "graphId", "status", "startedAtUtc", "completedAtUtc",
					"timeoutSeconds", "completedStepCount", "totalStepCount", "expectedRevision", "errorCode", "stepSummaries"
				}, StringComparer.Ordinal);

				Assert.That(migrated.Properties().All(value => allowed.Contains(value.Name)), Is.True, migrated.ToString());
				Assert.That(migrated.ToString(), Does.Not.Contain("approvalToken"));
				Assert.That(migrated.ToString(), Does.Not.Contain("delegatedResult"));
				Assert.That(migrated.ToString(), Does.Not.Contain("secret"));
				Assert.That(store.Count, Is.EqualTo(1));
				Assert.That(Directory.GetFiles(temporaryDirectory, "history.jsonl.tmp-*").Length, Is.Zero);
			}
			finally
			{
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		[Test]
		public void LegacyHistory_AllValidRowsMigrateWithoutLosingRows()
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			try
			{
				string historyPath = Path.Combine(temporaryDirectory, "history.jsonl");
				string[] legacyLines = Enumerable.Range(1, 3)
					.Select(index => BuildLegacyHistoryLine(index, true))
					.ToArray();
				File.WriteAllText(historyPath, string.Join(Environment.NewLine, legacyLines) + Environment.NewLine);

				AgentExecutionHistoryStore.StorageRootOverrideForTests = temporaryDirectory;
				AgentExecutionHistoryStore store = new AgentExecutionHistoryStore();
				store.Load();

				Assert.That(store.LastDiagnosticCode, Is.Null);
				Assert.That(store.Count, Is.EqualTo(3));
				string migratedText = File.ReadAllText(historyPath);
				Assert.That(migratedText, Does.Not.Contain("approvalToken"));
				Assert.That(migratedText, Does.Not.Contain("parameters"));
				Assert.That(migratedText, Does.Not.Contain("delegatedResult"));
				Assert.That(migratedText, Does.Not.Contain("message"));
				Assert.That(File.ReadLines(historyPath).Count(), Is.EqualTo(3));
				Assert.That(Directory.GetFiles(temporaryDirectory, "history.jsonl.tmp-*").Length, Is.Zero);
			}
			finally
			{
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		[TestCase(0)]
		[TestCase(2)]
		[TestCase(4)]
		public void LegacyHistory_CorruptedRowPreservesOriginalFile(int corruptedIndex)
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			try
			{
				string historyPath = Path.Combine(temporaryDirectory, "history.jsonl");
				string[] legacyLines = Enumerable.Range(1, 5)
					.Select(index => BuildLegacyHistoryLine(index, false))
					.ToArray();
				legacyLines[corruptedIndex] = "{ corrupted history row";
				File.WriteAllText(historyPath, string.Join(Environment.NewLine, legacyLines) + Environment.NewLine);
				string originalText = File.ReadAllText(historyPath);

				AgentExecutionHistoryStore.StorageRootOverrideForTests = temporaryDirectory;
				AgentExecutionHistoryStore store = new AgentExecutionHistoryStore();
				store.Load();

				Assert.That(store.LastDiagnosticCode, Is.EqualTo("AGENT-HISTORY-PERSISTENCE-FAILED"));
				Assert.That(store.Count, Is.Zero);
				Assert.That(File.ReadAllText(historyPath), Is.EqualTo(originalText));
				Assert.That(Directory.GetFiles(temporaryDirectory, "history.jsonl.tmp-*").Length, Is.Zero);
			}
			finally
			{
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		[Test]
		public void Trace_UsesAllowlistSequenceAndExactlyOneTerminalEvent()
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			try
			{
				AgentExecutionHistoryStore.StorageRootOverrideForTests = temporaryDirectory;
				AgentExecutionTraceStore.StorageRootOverrideForTests = temporaryDirectory;
				UnityAgentMcpRuntime.Instance.ResetPersistenceForTests();
				long revision = Session.Revision;
				JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
				JObject started = UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);
				UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();

				JObject[] events = File.ReadLines(UnityAgentMcpRuntime.TracePathForTests).Select(JObject.Parse).ToArray();
				Assert.That(events.Select(value => value.Value<string>("event")).ToArray(), Is.EqualTo(new[]
					{"EXECUTION_STARTED", "STEP_STARTED", "STEP_COMPLETED", "EXECUTION_COMPLETED"}));
				HashSet<string> terminalEvents = new HashSet<string>(new[]
					{"EXECUTION_COMPLETED", "EXECUTION_FAILED", "EXECUTION_PARTIAL", "EXECUTION_CANCELLED", "EXECUTION_INTERRUPTED"}, StringComparer.Ordinal);
				Assert.That(events.Count(value => terminalEvents.Contains(value.Value<string>("event"))), Is.EqualTo(1));
				HashSet<string> allowed = new HashSet<string>(new[]
					{"schemaVersion", "timestampUtc", "executionId", "graphId", "stepId", "domainId", "toolName", "event", "revision", "resultCode", "durationMs"}, StringComparer.Ordinal);
				Assert.That(events.All(value => value.Properties().All(property => allowed.Contains(property.Name))), Is.True);
				Assert.That(events[0]["stepId"].Type, Is.EqualTo(JTokenType.Null));
				Assert.That(events[0].Value<double>("durationMs"), Is.EqualTo(0.0));
				Assert.That(events[0].Value<long>("revision"), Is.EqualTo(revision));
				Assert.That(events[2].Value<double>("durationMs"), Is.GreaterThanOrEqualTo(0.0));
				Assert.That(events[3].Value<double>("durationMs"), Is.GreaterThanOrEqualTo(0.0));
				Assert.That(events[3].Value<string>("resultCode"), Is.EqualTo("SUCCEEDED"));
				Assert.That(events.Select(value => value.ToString()).All(value => !value.Contains("approvalToken") && !value.Contains("parameters")), Is.True);
				Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"));
			}
			finally
			{
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		[Test]
		public void PersistenceFailure_DoesNotRewriteSuccessfulExecutionAsPartial()
		{
			string temporaryDirectory = CreateTemporaryDirectory();
			string blocker = Path.Combine(temporaryDirectory, "not-a-directory");
			File.WriteAllText(blocker, "blocker");
			try
			{
				AgentExecutionHistoryStore.StorageRootOverrideForTests = blocker;
				AgentExecutionTraceStore.StorageRootOverrideForTests = blocker;
				UnityAgentMcpRuntime.Instance.ResetPersistenceForTests();
				long revision = Session.Revision;
				JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
				JObject started = UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);
				UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
				JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

				Assert.That(status.Value<string>("status"), Is.EqualTo("SUCCEEDED"), status.ToString());
				Assert.That(status.Value<bool>("executionSucceeded"), Is.True);
				Assert.That(status.Value<bool>("success"), Is.True);
			}
			finally
			{
				DeleteTemporaryDirectory(temporaryDirectory);
			}
		}

		private static string BuildLegacyHistoryLine(int index, bool includeUnsafeFields)
		{
			JObject entry = new JObject
			{
				["executionId"] = "legacy-execution-" + index,
				["graphId"] = "legacy-graph-" + index,
				["status"] = "SUCCEEDED",
				["startedAtUtc"] = "2026-01-01T00:00:00.0000000Z",
				["completedAtUtc"] = "2026-01-01T00:00:01.0000000Z",
				["timeoutSeconds"] = 60,
				["completedStepCount"] = 1,
				["totalStepCount"] = 1,
				["expectedRevision"] = 7,
				["errorCode"] = null,
				["stepSummaries"] = new JArray(new JObject
				{
					["stepId"] = "step-" + index,
					["domainId"] = "domain",
					["toolName"] = "tool",
					["resultCode"] = "SUCCEEDED",
					["durationMs"] = 1.0
				})
			};
			if (includeUnsafeFields)
			{
				entry["message"] = "C:\\secret\\input";
				entry["approvalToken"] = "token";
				entry["params"] = new JObject { ["password"] = "secret" };
				entry["delegatedResult"] = new JObject { ["raw"] = "payload" };
				entry["stepResults"] = new JArray(new JObject { ["raw"] = "payload" });
			}
			return entry.ToString(Formatting.None);
		}

		private static string CreateTemporaryDirectory()
		{
			string path = Path.Combine(Path.GetTempPath(), "MyUnityMcpAgentTests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return path;
		}

		private static void DeleteTemporaryDirectory(string path)
		{
			if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
			{
				Directory.Delete(path, true);
			}
		}

		[Test]
		public void ValidateWorkflow_AcceptsIntegrationCandidateDomain()
		{
			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[]
			{
				new UnityAgentMcpStepInput
				{
					stepId = "ui",
					domainId = "unity_ui_mcp",
					toolName = "ui.inspect",
					toolGroup = "ui"
				}
			});

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("valid"), Is.True);
		}

		[Test]
		public void ValidateWorkflow_RejectsMissingToolGroup()
		{
			UnityAgentMcpStepInput step = GraphicsInspectStep();
			step.toolGroup = "missing";

			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[] {step});

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-TOOL-GROUP-MISSING"));
		}

		[Test]
		public void ValidateWorkflow_RejectsKnownButWrongCanonicalToolGroup()
		{
			UnityAgentMcpStepInput step = GraphicsInspectStep();
			step.toolGroup = "mutate";

			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[] {step});

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-TOOL-GROUP-MISMATCH"));
		}

		[Test]
		public void ValidateWorkflow_RejectsCycle()
		{
			UnityAgentMcpStepInput first = GraphicsInspectStep("first", "second");
			UnityAgentMcpStepInput second = GraphicsInspectStep("second", "first");

			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[] {first, second});

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-GRAPH-CYCLE"));
		}

		[Test]
		public void CompilePreviewStart_DelegatesExistingGraphicsInspectionCooperatively()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			string graphId = compiled.Value<string>("graphId");
			JObject preview = UnityAgentMcpRuntime.Instance.PreviewExecution(graphId);
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(graphId, revision, null);

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(preview.Value<string>("status"), Is.EqualTo("PREVIEW"));
			Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"), started.ToString());

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject completed = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));
			Assert.That(completed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), completed.ToString());
			Assert.That(completed.Value<bool>("executionSucceeded"), Is.True);
			Assert.That(completed["stepResults"]?.Count(), Is.EqualTo(1));
		}

		[Test]
		public void IntegrationCandidate_DelegatesProfilerInspection()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {ProfilerInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject completed = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(completed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), completed.ToString());
			Assert.That(completed.Value<bool>("executionSucceeded"), Is.True);
			Assert.That(completed.Value<bool>("success"), Is.True);
			Assert.That(((JObject)completed["stepResults"][0]).Value<string>("resultCode"), Is.EqualTo("SUCCEEDED"), completed.ToString());
		}

		[Test]
		public void CompileGraph_RejectsRevisionThatIsNotCurrentEditorRevision()
		{
			long revision = Session.Revision;
			JObject result = UnityAgentMcpRuntime.Instance.CompileGraph(revision + 1, new[] {GraphicsInspectStep()});

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void StartExecution_RejectsChangedCallerRevision()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision + 1,
				null);

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void StartExecution_RejectsActualEditorRevisionChangeEvenWithOldCallerValue()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			Session.NotifyMutationApplied();
			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void PreviewExecution_RejectsGraphAfterEditorRevisionChanges()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			Session.NotifyMutationApplied();
			JObject result = UnityAgentMcpRuntime.Instance.PreviewExecution(compiled.Value<string>("graphId"));

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void MutationGroup_RequiresAgentApprovalThenDelegatesToDomainSafety()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsMutationStep()});
			string graphId = compiled.Value<string>("graphId");

			JObject missing = UnityAgentMcpRuntime.Instance.StartExecution(graphId, revision, null);
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject delegated = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(missing.Value<string>("errorCode"), Is.EqualTo("AGENT-APPROVAL-MISSING-OR-EXPIRED"));
			Assert.That(approval.Value<bool>("success"), Is.True);
			Assert.That(delegated.Value<string>("status"), Is.EqualTo("FAILED"), delegated.ToString());
			Assert.That(delegated.Value<string>("errorCode"), Is.Not.EqualTo("AGENT-DELEGATE-NOT-REGISTERED"));
		}

		[Test]
		public void Approval_ExpiresBeforeExecutionStarts()
		{
			DateTime now = DateTime.UtcNow;
			UnityAgentMcpRuntime.UtcNowOverrideForTests = () => now;
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsMutationStep()});
			string graphId = compiled.Value<string>("graphId");
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			now = now.AddMinutes(11);
			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-APPROVAL-MISSING-OR-EXPIRED"));
		}

		[Test]
		public void LaterDelegateFailure_ProducesPartialInsteadOfFalseSuccess()
		{
			long revision = Session.Revision;
			UnityAgentMcpStepInput inspect = GraphicsInspectStep("inspect");
			UnityAgentMcpStepInput mutate = GraphicsMutationStep("mutate", "inspect");
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {inspect, mutate});
			string graphId = compiled.Value<string>("graphId");
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject result = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(result.Value<string>("status"), Is.EqualTo("PARTIAL"), result.ToString());
			Assert.That(result.Value<bool>("executionSucceeded"), Is.False);
			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result["stepResults"]?.Count(), Is.EqualTo(2));
		}

		[Test]
		public void CancelExecution_StopsQueuedExecutionBeforeDelegation()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[]
			{
				GraphicsInspectStep("first"),
				GraphicsInspectStep("second", "first")
			});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);
			JObject cancelled = UnityAgentMcpRuntime.Instance.CancelExecution(started.Value<string>("executionId"));
			JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"));
			Assert.That(cancelled.Value<bool>("success"), Is.True);
			Assert.That(cancelled.Value<string>("status"), Is.EqualTo("CANCELLED"));
			Assert.That(status.Value<string>("status"), Is.EqualTo("CANCELLED"));
			Assert.That(status.Value<bool>("success"), Is.False);
			Assert.That(status.Value<bool>("executionSucceeded"), Is.False);
			Assert.That(status.Value<int>("completedStepCount"), Is.Zero);
		}

		[Test]
		public void ExecutionTimeout_InterruptsBeforeNextStep()
		{
			DateTime now = DateTime.UtcNow;
			UnityAgentMcpRuntime.UtcNowOverrideForTests = () => now;
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null,
				1);
			now = now.AddSeconds(2);
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(status.Value<string>("status"), Is.EqualTo("INTERRUPTED"));
			Assert.That(status.Value<string>("errorCode"), Is.EqualTo("AGENT-EXECUTION-TIMEOUT"));
			Assert.That(status.Value<bool>("success"), Is.False);
			Assert.That(status.Value<int>("completedStepCount"), Is.Zero);
		}

		[Test]
		public void ClientDisconnect_InterruptsRunningExecution()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);

			UnityAgentMcpRuntime.Instance.NotifyClientDisconnected();
			JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(status.Value<string>("status"), Is.EqualTo("INTERRUPTED"));
			Assert.That(status.Value<string>("errorCode"), Is.EqualTo("AGENT-CLIENT-DISCONNECTED"));
		}

		[Test]
		public void ExecutionHistory_ContainsCompletedExecution()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject history = UnityAgentMcpRuntime.Instance.GetExecutionHistory(100);

			Assert.That(UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId")).Value<string>("status"), Is.EqualTo("SUCCEEDED"));
			Assert.That(history.Value<bool>("success"), Is.True);
			Assert.That(history.Value<int>("total"), Is.GreaterThan(0));
		}

		[Test]
		public void ErrorCatalog_DeclaresTimeoutAndDisconnectRecovery()
		{
			JObject result = UnityAgentMcpRuntime.Instance.GetErrorCatalog();
			string[] codes = result["errors"]?.Values<JObject>()
				.Select(value => value.Value<string>("code"))
				.ToArray();

			CollectionAssert.Contains(codes, "AGENT-EXECUTION-TIMEOUT");
			CollectionAssert.Contains(codes, "AGENT-CLIENT-DISCONNECTED");
			CollectionAssert.Contains(codes, "AGENT-TIMEOUT-INVALID");
		}

		[Test]
		public void AgentTools_AreDefaultDisabled()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpTools.cs");

			Assert.That(source.Split(new[] {"[McpForUnityTool("}, StringSplitOptions.None).Length - 1, Is.EqualTo(10));
			Assert.That(source.Split(new[] {"AutoRegister = false"}, StringSplitOptions.None).Length - 1, Is.EqualTo(10));
		}

		[Test]
		public void ControlPlaneSource_DoesNotCallDirectMutationApis()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpRuntime.cs");

			Assert.That(source, Does.Not.Contain("Undo.RecordObject"));
			Assert.That(source, Does.Not.Contain("EditorUtility.SetDirty"));
			Assert.That(source, Does.Not.Contain("AssetDatabase.CreateAsset"));
			Assert.That(source, Does.Not.Contain("EditorSceneManager.SaveScene"));
		}
	}
}

#endif
