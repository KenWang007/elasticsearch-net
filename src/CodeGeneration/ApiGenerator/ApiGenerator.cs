﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ApiGenerator.Domain;
using Newtonsoft.Json.Linq;
using RazorLight;
using ShellProgressBar;

namespace ApiGenerator
{
	public class ApiGenerator
	{
		private static readonly RazorLightEngine Razor = new RazorLightEngineBuilder()
			.UseMemoryCachingProvider()
			.Build();

		public static List<string> Warnings { get; private set; }

		private static string[] IgnoredApis { get; } =
		{
			"xpack.ml.delete_filter.json",
			"xpack.ml.get_filters.json",
			"xpack.ml.put_filter.json",
			//"rank_eval.json",

			// these API's are new and need to be mapped
			"xpack.license.get_basic_status.json",
			"xpack.license.post_start_basic.json",
			"xpack.ml.delete_calendar.json",
			"xpack.ml.delete_calendar_event.json",
			"xpack.ml.delete_calendar_job.json",
			"xpack.ml.get_calendar_events.json",
			"xpack.ml.get_calendars.json",
			"xpack.ml.info.json",
			"xpack.ml.post_calendar_events.json",
			"xpack.ml.put_calendar.json",
			"xpack.ml.put_calendar_job.json",
			"xpack.ml.get_calendar_job.json",
			"xpack.ssl.certificates.json",

			// 6.4 new  API's
			"xpack.ml.update_filter.json",
			"xpack.security.delete_privileges.json",
			"xpack.security.get_privileges.json",
			"xpack.security.has_privileges.json",
			"xpack.security.put_privilege.json",
			"xpack.security.put_privileges.json",
			"nodes.reload_secure_settings.json"
		};

		public static void Generate(string downloadBranch, params string[] folders)
		{
			Warnings = new List<string>();
			var spec = CreateRestApiSpecModel(downloadBranch, folders);
			var actions = new Dictionary<Action<RestApiSpec>, string>
			{
				{ GenerateClientInterface, "Client interface" },
				{ GenerateRequestParameters, "Request parameters" },
				{ GenerateDescriptors, "Descriptors" },
				{ GenerateRequests, "Requests" },
				{ GenerateEnums, "Enums" },
				{ GenerateRawClient, "Lowlevel client" },
				{ GenerateRawDispatch, "Dispatch" },
			};

			using (var pbar = new ProgressBar(actions.Count, "Generating code", new ProgressBarOptions { BackgroundColor = ConsoleColor.DarkGray }))
			{
				foreach (var kv in actions)
				{
					pbar.Message = "Generating " + kv.Value;
					kv.Key(spec);
					pbar.Tick("Generated " + kv.Value);
				}
			}

			if (Warnings.Count == 0) return;

			Console.ForegroundColor = ConsoleColor.Yellow;
			foreach (var warning in Warnings.Distinct().OrderBy(w => w))
				Console.WriteLine(warning);
			Console.ResetColor();
		}

		private static RestApiSpec CreateRestApiSpecModel(string downloadBranch, string[] folders)
		{
			var directories = Directory.GetDirectories(CodeConfiguration.RestSpecificationFolder, "*", SearchOption.AllDirectories)
				.Where(f => folders == null || folders.Length == 0 || folders.Contains(new DirectoryInfo(f).Name))
				.ToList();

			var endpoints = new Dictionary<string, ApiEndpoint>();
			using (var pbar = new ProgressBar(directories.Count, $"Listing {directories.Count} directories",
				new ProgressBarOptions { BackgroundColor = ConsoleColor.DarkGray }))
			{
				var folderFiles = directories.Select(dir =>
					Directory.GetFiles(dir)
						.Where(f => f.EndsWith(".json") && !IgnoredApis.Contains(new FileInfo(f).Name))
						.ToList()
				);
				var commonFile = Path.Combine(CodeConfiguration.RestSpecificationFolder, "Core", "_common.json");
				if (!File.Exists(commonFile)) throw new Exception($"Expected to find {commonFile}");

				RestApiSpec.CommonApiQueryParameters = CreateCommonApiQueryParameters(commonFile);

				foreach (var jsonFiles in folderFiles)
				{
					using (var fileProgress = pbar.Spawn(jsonFiles.Count, $"Listing {jsonFiles.Count} files",
						new ProgressBarOptions { ProgressCharacter = '─', BackgroundColor = ConsoleColor.DarkGray }))
					{
						foreach (var file in jsonFiles)
						{
							if (file.EndsWith("_common.json")) continue;
							else if (file.EndsWith(".obsolete.json")) continue;
							else if (file.EndsWith(".patch.json")) continue;
							else if (file.EndsWith(".replace.json")) continue;
							else
							{
								var endpoint = CreateApiEndpoint(file);
								endpoints.Add(endpoint.Key, endpoint.Value);
							}

							fileProgress.Tick();
						}
					}
					pbar.Tick();
				}
			}

			return new RestApiSpec { Endpoints = endpoints, Commit = downloadBranch };
		}

		public static string PascalCase(string s)
		{
			var textInfo = new CultureInfo("en-US").TextInfo;
			return textInfo.ToTitleCase(s.ToLowerInvariant()).Replace("_", string.Empty).Replace(".", string.Empty);
		}

		private static KeyValuePair<string, ApiEndpoint> CreateApiEndpoint(string jsonFile)
		{
			var replaceFile = Path.Combine(Path.GetDirectoryName(jsonFile), Path.GetFileNameWithoutExtension(jsonFile)) + ".replace.json";
			if (File.Exists(replaceFile))
			{
				var replaceSpec = JObject.Parse(File.ReadAllText(replaceFile));
				var endpointReplaced = replaceSpec.ToObject<Dictionary<string, ApiEndpoint>>().First();
				endpointReplaced.Value.RestSpecName = endpointReplaced.Key;
				endpointReplaced.Value.CsharpMethodName = CreateMethodName(endpointReplaced.Key);
				return endpointReplaced;
			}

			var officialJsonSpec = JObject.Parse(File.ReadAllText(jsonFile));
			PatchOfficialSpec(officialJsonSpec, jsonFile);
			var endpoint = officialJsonSpec.ToObject<Dictionary<string, ApiEndpoint>>().First();
			endpoint.Value.RestSpecName = endpoint.Key;
			endpoint.Value.CsharpMethodName = CreateMethodName(endpoint.Key);
			return endpoint;
		}

		private static void PatchOfficialSpec(JObject original, string jsonFile)
		{
			var directory = Path.GetDirectoryName(jsonFile);
			var patchFile = Path.Combine(directory, Path.GetFileNameWithoutExtension(jsonFile)) + ".patch.json";
			if (!File.Exists(patchFile)) return;

			var patchedJson = JObject.Parse(File.ReadAllText(patchFile));

			var pathsOverride = patchedJson.SelectToken("*.url.paths");

			original.Merge(patchedJson, new JsonMergeSettings
			{
				MergeArrayHandling = MergeArrayHandling.Union
			});

			if (pathsOverride != null) original.SelectToken("*.url.paths").Replace(pathsOverride);
		}

		private static Dictionary<string, ApiQueryParameters> CreateCommonApiQueryParameters(string jsonFile)
		{
			var json = File.ReadAllText(jsonFile);
			var jobject = JObject.Parse(json);
			var commonParameters = jobject.Property("params").Value.ToObject<Dictionary<string, ApiQueryParameters>>();
			return ApiQueryParametersPatcher.Patch(null, commonParameters, null, false);
		}

		private static string CreateMethodName(string apiEndpointKey) => PascalCase(apiEndpointKey);

		private static string DoRazor(string name, string template, RestApiSpec model) =>
			Razor.CompileRenderAsync(name, template, model).GetAwaiter().GetResult();

		private static void GenerateClientInterface(RestApiSpec model)
		{
			var targetFile = CodeConfiguration.EsNetFolder + @"IElasticLowLevelClient.Generated.cs";
			var source = DoRazor(nameof(GenerateClientInterface),
				File.ReadAllText(CodeConfiguration.ViewFolder + @"IElasticLowLevelClient.Generated.cshtml"), model);
			File.WriteAllText(targetFile, source);
		}

		private static void GenerateRawDispatch(RestApiSpec model)
		{
			var targetFile = CodeConfiguration.NestFolder + @"_Generated/_LowLevelDispatch.Generated.cs";
			var source = DoRazor(nameof(GenerateRawDispatch), File.ReadAllText(CodeConfiguration.ViewFolder + @"_LowLevelDispatch.Generated.cshtml"),
				model);
			File.WriteAllText(targetFile, source);
		}

		private static void GenerateRawClient(RestApiSpec model)
		{
			var targetFile = CodeConfiguration.EsNetFolder + @"ElasticLowLevelClient.Generated.cs";
			var source = DoRazor(nameof(GenerateRawClient),
				File.ReadAllText(CodeConfiguration.ViewFolder + @"ElasticLowLevelClient.Generated.cshtml"), model);
			File.WriteAllText(targetFile, source);
		}

		private static void GenerateDescriptors(RestApiSpec model)
		{
			var targetFile = CodeConfiguration.NestFolder + @"_Generated\_Descriptors.Generated.cs";
			var source = DoRazor(nameof(GenerateDescriptors), File.ReadAllText(CodeConfiguration.ViewFolder + @"_Descriptors.Generated.cshtml"),
				model);
			File.WriteAllText(targetFile, source);
		}

		private static void GenerateRequests(RestApiSpec model)
		{
			var targetFile = CodeConfiguration.NestFolder + @"_Generated\_Requests.Generated.cs";
			var source = DoRazor(nameof(GenerateRequests), File.ReadAllText(CodeConfiguration.ViewFolder + @"_Requests.Generated.cshtml"), model);
			File.WriteAllText(targetFile, source);
		}

		private static void GenerateRequestParameters(RestApiSpec model)
		{
			var targetFile = CodeConfiguration.EsNetFolder + @"Domain\RequestParameters\RequestParameters.Generated.cs";
			var source = DoRazor(nameof(GenerateRequestParameters),
				File.ReadAllText(CodeConfiguration.ViewFolder + @"RequestParameters.Generated.cshtml"), model);
			File.WriteAllText(targetFile, source);
		}

		private static void GenerateEnums(RestApiSpec model)
		{
			var targetFile = CodeConfiguration.EsNetFolder + @"Domain\Enums.Generated.cs";
			var source = DoRazor(nameof(GenerateEnums), File.ReadAllText(CodeConfiguration.ViewFolder + @"Enums.Generated.cshtml"), model);
			File.WriteAllText(targetFile, source);
		}
	}
}
