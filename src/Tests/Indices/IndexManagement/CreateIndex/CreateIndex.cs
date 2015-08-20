﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Framework;
using Tests.Framework.MockData;
using Nest;
using Elasticsearch.Net;
using Xunit;
using Tests.Framework.Integration;

namespace Tests.Indices.IndexManagement
{
	[Collection(IntegrationContext.Indexing)]
	public class CreateIndex : ApiCallIntegration<IIndicesOperationResponse, ICreateIndexRequest, CreateIndexDescriptor, CreateIndexRequest>
	{
		public CreateIndex(IndexingCluster cluster, ApiUsage usage) : base(cluster, usage) { }

		public string IndexName { get; } = RandomString();

		protected override LazyResponses ClientUsage() => Calls(
			fluent: (client, f) => client.CreateIndex(IndexName, f),
			fluentAsync: (client, f) => client.CreateIndexAsync(this.IndexName, f),
			request: (client, r) => client.CreateIndex(r),
			requestAsync: (client, r) => client.CreateIndexAsync(r)
		);
		protected override void OnBeforeCall(IElasticClient client)
		{
			if (client.IndexExists(IndexName).Exists) client.DeleteIndex(IndexName);
		}

		public override bool ExpectIsValid => true;
		public override int ExpectStatusCode => 200;
		public override HttpMethod HttpMethod => HttpMethod.POST;
		public override string UrlPath => $"/{IndexName}";

		protected override object ExpectJson { get; } = new
		{
			settings = new Dictionary<string, object>
			{
				{ "index.number_of_replicas", 1 },
				{ "index.number_of_shards", 1 },
			}
		};

		protected override Func<CreateIndexDescriptor, ICreateIndexRequest> Fluent => d => d
			.Settings(s => s
				.NumberOfReplicas(1)
				.NumberOfShards(1)
			)
			.Analysis(a => a
				.Analyzers(an => an
				)
			);

		protected override CreateIndexRequest Initializer => new CreateIndexRequest(this.IndexName)
		{
			Settings = new IndexSettings()
			{
				NumberOfReplicas = 1,
				NumberOfShards = 1,
			}
		};
	}

	[Collection(IntegrationContext.ReadOnly)]
	public class CreateIndexFullExample : ApiCallExample<IIndicesOperationResponse, ICreateIndexRequest, CreateIndexDescriptor, CreateIndexRequest>
	{
		public CreateIndexFullExample(ReadOnlyCluster cluster, ApiUsage usage) : base(cluster, usage) { }

		public static string IndexName { get; } = RandomString();

		public override HttpMethod HttpMethod => HttpMethod.POST;
		public override string UrlPath => $"/{IndexName}";

		protected override LazyResponses ClientUsage() => Calls(
			fluent: (client, f) => client.CreateIndex(IndexName, f),
			fluentAsync: (client, f) => client.CreateIndexAsync(IndexName, f),
			request: (client, r) => client.CreateIndex(r),
			requestAsync: (client, r) => client.CreateIndexAsync(r)
		);

		protected override object ExpectJson { get; } = new
		{
			settings = new Dictionary<string, object>
			{
				{"any.setting", "can be set"},
				{"index.number_of_replicas", 2},
				{"index.auto_expand_replicas", "1-3" },
				{"index.refresh_interval", -1 },
				{"index.blocks.read_only", true},
				{"index.blocks.read", true},
				{"index.blocks.write", true},
				{"index.blocks.metadata", true},
				{"index.priority", 11},
				{"index.recovery.initial_shards", "full-1"},
				{"index.routing.allocation.total_shards_per_node", 10 },
				{"index.unassigned.node_left.delayed_timeout", "1m" },
				{ "index.translog.sync_interval", "5s" },
				{"index.translog.durability", "request" },
				{"index.translog.fs.type", "buffered" },
				{"index.translog.flush_threshold_size", "10mb" },
				{"index.translog.flush_threshold_ops", 2 },
				{"index.translog.flush_threshold_period", "30m" },
				{"index.translog.interval", "5s" },
				{"index.merge.policy.expunge_deletes_allowed", 10},
				{"index.merge.policy.floor_segment", "2mb"},
				{"index.merge.policy.max_merge_at_once", 10},
				{"index.merge.policy.max_merge_at_once_explicit", 30},
				{"index.merge.policy.max_merged_segment", "5gb"},
				{"index.merge.policy.segments_per_tier", 10},
				{"index.merge.policy.reclaim_deletes_weight", 2.0},
				{"index.merge.scheduler.max_thread_count", 1},
				{"index.merge.scheduler.auto_throttle", true},
				{"index.search.slowlog.threshold.query.warn", "10s"},
				{"index.search.slowlog.threshold.query.info", "5s"},
				{"index.search.slowlog.threshold.query.debug", "2s"},
				{"index.search.slowlog.threshold.query.trace", "500ms"},
				{"index.search.slowlog.threshold.fetch.warn", "1s"},
				{"index.search.slowlog.threshold.fetch.info", "800ms"},
				{"index.search.slowlog.threshold.fetch.debug", "500ms"},
				{"index.search.slowlog.threshold.fetch.trace", "200ms"},
				{"index.search.slowlog.level", 1},
				{"index.indexing.slowlog.threshold.index.warn", "10s"},
				{"index.indexing.slowlog.threshold.index.info", "5s"},
				{"index.indexing.slowlog.threshold.index.debug", "2s"},
				{"index.indexing.slowlog.threshold.index.trace", "500ms"},
				{"index.indexing.slowlog.level", 2},
				{"index.indexing.slowlog.source", 100},
				{"index.number_of_shards", 1},
				{"index.store.type", "mmapfs"},
			}
		};

		protected override CreateIndexDescriptor ClientDoesThisInternally(CreateIndexDescriptor d) =>
			d.Index(IndexName);

		protected override Func<CreateIndexDescriptor, ICreateIndexRequest> Fluent => d => d
			.Settings(s => s
				.Add("any.setting", "can be set")
				.NumberOfShards(1)
				.NumberOfReplicas(2)
				.AutoExpandReplicas("1-3")
				.BlocksMetadata()
				.BlocksRead()
				.BlocksReadOnly()
				.BlocksWrite()
				.Priority(11)
				.RecoveryInitialShards(RecoveryInitialShards.FullMinusOne)
				.TotalShardsPerNode(10)
				.UnassignedNodeLeftDelayedTimeout(TimeSpan.FromMinutes(1))
				.RefreshInterval(-1)
				.FileSystemStorageImplementation(FileSystemStorageImplementation.MMap)
				.Merge(m => m
					.Policy(p => p
						.ExpungeDeletesAllowed(10)
						.FloorSegment("2mb")
						.MaxMergeAtOnce(10)
						.MaxMergeAtOnceExplicit(30)
						.MaxMergedSegement("5gb")
						.SegmentsPerTier(10)
						.ReclaimDeletesWeight(2.0)
					)
					.Scheduler(schedule => schedule
						.AutoThrottle()
						.MaxThreadCount(1)
					)
				)
				.SlowLog(l => l
					.Indexing(i => i
						.TresholdWarn("10s")
						.TresholdInfo("5s")
						.TresholdDebug(TimeSpan.FromSeconds(2))
						.TresholdTrace(TimeSpan.FromMilliseconds(500))
						.LogLevel(SlowLogLevel.Debug)
						.Source(100)
					)
					.Search(search => search
						.Query(q => q
							.TresholdWarn("10s")
							.TresholdInfo("5s")
							.TresholdDebug(TimeSpan.FromSeconds(2))
							.TresholdTrace(TimeSpan.FromMilliseconds(500))
						)
						.Fetch(f => f
							.TresholdWarn("1s")
							.TresholdInfo("800ms")
							.TresholdDebug(TimeSpan.FromMilliseconds(500))
							.TresholdTrace(TimeSpan.FromMilliseconds(200))
						)
						.LogLevel(SlowLogLevel.Info)
					)
				)
				.Translog(t=>t
					.Flush(f=>f
						.TresholdOps(2)
						.TresholdSize("10mb")
						.TresholdPeriod(TimeSpan.FromMinutes(30))
						.Interval(TimeSpan.FromSeconds(5))
					)
					.SyncInterval("5s")
					.Durability(TranslogDurability.Request)
					.FileSystemType(TranslogWriteMode.Buffered)
				)
			)
			.Analysis(a => a
				.Analyzers(an => an
				)
			);

		protected override CreateIndexRequest Initializer => new CreateIndexRequest(IndexName)
		{
		};
	}
}
