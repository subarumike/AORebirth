using System;
using System.Globalization;
using System.IO;
using System.Text;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;

namespace ZoneEngine.Core.Playfields;

internal sealed class SubwayVisibilityDiagnosticSnapshot
{
	private readonly object sync = new object();

	private readonly SubwayVisibilityDiagnosticConfiguration configuration;

	private readonly Identity playerIdentity;

	private readonly int playfieldId;

	private readonly DateTime startedUtc;

	private readonly string snapshotId;

	private readonly string eventPath;

	private readonly string ledgerPath;

	private readonly string summaryPath;

	private int sendOrdinal;

	private int totalCandidateNpcs;

	private SubwayVisibilitySpatialInterestMetrics spatialInterestMetrics;

	private int totalNpcsSent;

	private int totalPackets;

	private long totalBytes;

	private int completedEnemies;

	private int largestScfu;

	private int largestEnemyTotal;

	private int lastCompletedOrdinal;

	private string lastCompletedIdentity = string.Empty;

	private DateTime? firstSendUtc;

	private DateTime? lastSendUtc;

	private bool enqueueCompleted;

	private bool failed;

	private bool finalized;

	internal SubwayVisibilityDiagnosticSnapshot(SubwayVisibilityDiagnosticConfiguration configuration, ICharacter recipient, int candidateCharacters)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		this.configuration = configuration;
		playerIdentity = ((IEntity)recipient).Identity;
		Identity identity = ((IEntity)((IInstancedEntity)recipient).Playfield).Identity;
		playfieldId = ((Identity)(ref identity)).Instance;
		startedUtc = DateTime.UtcNow;
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		string sessionId = configuration.SessionId;
		identity = ((IEntity)recipient).Identity;
		snapshotId = string.Format(invariantCulture, "{0}-{1:X8}-{2:yyyyMMddTHHmmssfff}", sessionId, ((Identity)(ref identity)).Instance, startedUtc);
		eventPath = Path.Combine(configuration.ArtifactDirectory, "runtime-events.jsonl");
		ledgerPath = Path.Combine(configuration.ArtifactDirectory, "per-enemy-send-ledger.csv");
		summaryPath = Path.Combine(configuration.ArtifactDirectory, "snapshot-summary.jsonl");
		Directory.CreateDirectory(configuration.ArtifactDirectory);
		WriteEvent("SNAPSHOT_STARTED", null, string.Empty, 0, "candidate_characters=" + candidateCharacters.ToString(CultureInfo.InvariantCulture));
	}

	internal SubwayVisibilityDiagnosticEnemy BeginEnemy(Character character)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || (((Dynel)character).Controller != null && ((Dynel)character).Controller.Client != null))
		{
			return null;
		}
		Identity identity = ((PooledObject)character).Identity;
		SubwayVisibilityDiagnosticSelection.TryGetRuntimeEntry(((Identity)(ref identity)).Instance, out var entry);
		Coordinate val = ((Dynel)character).Coordinates();
		int num;
		lock (sync)
		{
			num = ++sendOrdinal;
		}
		int num2 = 0;
		try
		{
			num2 = ((Dynel)character).Stats[(StatIds)54].Value;
		}
		catch
		{
			num2 = 0;
		}
		SubwayVisibilityDiagnosticEnemy subwayVisibilityDiagnosticEnemy = new SubwayVisibilityDiagnosticEnemy(num, ((PooledObject)character).Identity, ((Dynel)character).Name, (entry == null) ? ((Dynel)character).Name : entry.Family, (entry == null) ? "BASELINE" : entry.Classification, entry?.Ordinal ?? 0, entry?.SourceInstance ?? 0, (entry == null) ? string.Empty : entry.SourceCapture, val.x, val.y, val.z, num2, DateTime.UtcNow);
		WriteEvent("ENEMY_SEQUENCE_STARTED", subwayVisibilityDiagnosticEnemy, string.Empty, 0, string.Empty);
		return subwayVisibilityDiagnosticEnemy;
	}

	internal void MarkEnemyQueued(SubwayVisibilityDiagnosticEnemy enemy)
	{
		if (enemy != null)
		{
			lock (sync)
			{
				totalNpcsSent++;
				enemy.CumulativeNpcCount = totalNpcsSent;
				enemy.CumulativePacketCount = totalPackets;
				enemy.CumulativeBytes = totalBytes;
			}
			WriteEvent("ENEMY_SEQUENCE_QUEUED", enemy, string.Empty, enemy.TotalBytes, string.Empty);
		}
	}

	internal void MarkWeaponPhaseStarted(SubwayVisibilityDiagnosticEnemy enemy)
	{
		if (enemy != null)
		{
			WriteEvent("WEAPON_ENUMERATION_STARTED", enemy, string.Empty, 0, string.Empty);
		}
	}

	internal void MarkWeaponPhaseCompleted(SubwayVisibilityDiagnosticEnemy enemy)
	{
		if (enemy != null)
		{
			WriteEvent("WEAPON_ENUMERATION_COMPLETED", enemy, string.Empty, enemy.WeaponBytes, "weapon_packet_count=" + enemy.WeaponCount.ToString(CultureInfo.InvariantCulture));
		}
	}

	internal void MarkSnapshotEnqueueCompleted()
	{
		lock (sync)
		{
			enqueueCompleted = true;
		}
		WriteEvent("SNAPSHOT_ENQUEUE_COMPLETED", null, string.Empty, 0, string.Empty);
		TryFinalize();
	}

	internal void SetTotalCandidateNpcs(int value)
	{
		lock (sync)
		{
			totalCandidateNpcs = Math.Max(0, value);
		}
		WriteEvent("SNAPSHOT_CANDIDATES_COUNTED", null, string.Empty, 0, "total_candidate_npcs=" + totalCandidateNpcs.ToString(CultureInfo.InvariantCulture));
	}

	internal void RecordSpatialInterestSelection(SubwayVisibilitySpatialInterestMetrics metrics)
	{
		if (metrics == null)
		{
			throw new ArgumentNullException("metrics");
		}
		lock (sync)
		{
			spatialInterestMetrics = metrics;
		}
		SetTotalCandidateNpcs(metrics.TotalPlayfieldNpcs);
		WriteEvent("SNAPSHOT_SPATIAL_INTEREST_SELECTED", null, string.Empty, 0, string.Join(" ", "total_playfield_characters=" + metrics.TotalPlayfieldCharacters.ToString(CultureInfo.InvariantCulture), "total_playfield_npcs=" + metrics.TotalPlayfieldNpcs.ToString(CultureInfo.InvariantCulture), "spatial_query_inspected_candidates=" + metrics.SpatialQueryInspectedCandidates.ToString(CultureInfo.InvariantCulture), "within_enter_radius_count=" + metrics.WithinEnterRadiusCount.ToString(CultureInfo.InvariantCulture), "already_visible_count=" + metrics.AlreadyVisibleCount.ToString(CultureInfo.InvariantCulture), "newly_visible_count=" + metrics.NewlyVisibleCount.ToString(CultureInfo.InvariantCulture), "leaving_visible_count=" + metrics.LeavingVisibleCount.ToString(CultureInfo.InvariantCulture), "filtered_out_count=" + metrics.FilteredOutCount.ToString(CultureInfo.InvariantCulture)));
	}

	internal void RecordPacketEvent(SubwayVisibilityDiagnosticPacketContext context, string suffix, MessageBody body, int size, string detail)
	{
		WriteEvent(PacketPrefix(context.Kind) + "_" + suffix, context.Enemy, (body == null) ? string.Empty : ((object)body).GetType().Name, size, detail);
	}

	internal void RecordSerializedPacket(SubwayVisibilityDiagnosticPacketRecord record, MessageBody body)
	{
		record.Enemy.AddPacket(record.Kind, record.SerializedSize);
		lock (sync)
		{
			totalPackets++;
			totalBytes += record.SerializedSize;
			if (record.Kind == SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate && record.SerializedSize > largestScfu)
			{
				largestScfu = record.SerializedSize;
			}
		}
		WriteEvent(PacketPrefix(record.Kind) + "_SERIALIZATION_COMPLETED", record.Enemy, (body == null) ? string.Empty : ((object)body).GetType().Name, record.SerializedSize, "weapon_index=" + record.WeaponIndex.ToString(CultureInfo.InvariantCulture));
	}

	internal void RecordTransportEvent(SubwayVisibilityDiagnosticPacketRecord record, string suffix, string detail)
	{
		DateTime utcNow = DateTime.UtcNow;
		lock (sync)
		{
			if (!firstSendUtc.HasValue)
			{
				firstSendUtc = utcNow;
			}
			lastSendUtc = utcNow;
		}
		WriteEvent(PacketPrefix(record.Kind) + "_" + suffix, record.Enemy, string.Empty, record.SerializedSize, detail);
	}

	internal void RecordPacketTransportCompleted(SubwayVisibilityDiagnosticPacketRecord record)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if (record.Kind != SubwayVisibilityDiagnosticPacketKind.CharInPlay)
		{
			return;
		}
		record.Enemy.CompletedUtc = DateTime.UtcNow;
		lock (sync)
		{
			completedEnemies++;
			lastCompletedOrdinal = record.Enemy.SendOrdinal;
			Identity runtimeIdentity = record.Enemy.RuntimeIdentity;
			lastCompletedIdentity = ((object)(Identity)(ref runtimeIdentity)).ToString();
			if (record.Enemy.TotalBytes > largestEnemyTotal)
			{
				largestEnemyTotal = record.Enemy.TotalBytes;
			}
		}
		WriteLedger(record.Enemy);
		WriteEvent("ENEMY_SEQUENCE_COMPLETED", record.Enemy, string.Empty, record.Enemy.TotalBytes, string.Empty);
		TryFinalize();
	}

	internal void RecordFailure(SubwayVisibilityDiagnosticEnemy enemy, string phase, Exception exception)
	{
		bool flag;
		lock (sync)
		{
			flag = !failed;
			failed = true;
		}
		if (flag)
		{
			if (enemy != null)
			{
				WriteLedger(enemy, completed: false, phase);
			}
			WriteEvent("SNAPSHOT_FAILURE", enemy, string.Empty, 0, "phase=" + phase + " exception=" + ((exception == null) ? string.Empty : exception.ToString()));
			WriteSummary(completed: false, "FAILED");
		}
	}

	private void TryFinalize()
	{
		bool flag;
		lock (sync)
		{
			flag = !finalized && !failed && enqueueCompleted && completedEnemies == totalNpcsSent;
			if (flag)
			{
				finalized = true;
			}
		}
		if (flag)
		{
			WriteEvent("SNAPSHOT_COMPLETED", null, string.Empty, 0, string.Empty);
			WriteSummary(completed: true, "COMPLETED");
		}
	}

	private void WriteEvent(string eventName, SubwayVisibilityDiagnosticEnemy enemy, string messageType, int size, string detail)
	{
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		DateTime utcNow = DateTime.UtcNow;
		int value;
		int value2;
		long value3;
		lock (sync)
		{
			value = enemy?.CumulativeNpcCount ?? totalNpcsSent;
			value2 = enemy?.CumulativePacketCount ?? totalPackets;
			value3 = enemy?.CumulativeBytes ?? totalBytes;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append('{');
		AppendJson(stringBuilder, "timestamp_utc", utcNow.ToString("o", CultureInfo.InvariantCulture), first: true);
		AppendJson(stringBuilder, "session_id", configuration.SessionId, first: false);
		AppendJson(stringBuilder, "snapshot_id", snapshotId, first: false);
		Identity runtimeIdentity = playerIdentity;
		AppendJson(stringBuilder, "player_identity", ((object)(Identity)(ref runtimeIdentity)).ToString(), first: false);
		AppendJson(stringBuilder, "playfield_id", playfieldId, first: false);
		AppendJson(stringBuilder, "snapshot_phase", "initial_visibility", first: false);
		AppendJson(stringBuilder, "selected_slice", configuration.Slice, first: false);
		AppendJson(stringBuilder, "event", eventName, first: false);
		AppendJson(stringBuilder, "send_ordinal", enemy?.SendOrdinal ?? 0, first: false);
		AppendJson(stringBuilder, "manifest_ordinal", enemy?.ManifestOrdinal ?? 0, first: false);
		string value4;
		if (enemy != null)
		{
			runtimeIdentity = enemy.RuntimeIdentity;
			value4 = ((object)(Identity)(ref runtimeIdentity)).ToString();
		}
		else
		{
			value4 = string.Empty;
		}
		AppendJson(stringBuilder, "enemy_identity", value4, first: false);
		string value5;
		if (enemy != null)
		{
			runtimeIdentity = enemy.RuntimeIdentity;
			IdentityType type = ((Identity)(ref runtimeIdentity)).Type;
			value5 = ((object)(IdentityType)(ref type)).ToString();
		}
		else
		{
			value5 = string.Empty;
		}
		AppendJson(stringBuilder, "enemy_type", value5, first: false);
		int value6;
		if (enemy != null)
		{
			runtimeIdentity = enemy.RuntimeIdentity;
			value6 = ((Identity)(ref runtimeIdentity)).Instance;
		}
		else
		{
			value6 = 0;
		}
		AppendJson(stringBuilder, "enemy_instance", value6, first: false);
		AppendJson(stringBuilder, "source_identity", (enemy == null || enemy.SourceInstance == 0) ? string.Empty : ("SimpleChar:" + enemy.SourceInstance.ToString("X8", CultureInfo.InvariantCulture)), first: false);
		AppendJson(stringBuilder, "enemy_name", (enemy == null) ? string.Empty : enemy.Name, first: false);
		AppendJson(stringBuilder, "enemy_family", (enemy == null) ? string.Empty : enemy.Family, first: false);
		AppendJson(stringBuilder, "source_population_group", (enemy == null) ? string.Empty : enemy.SourceGroup, first: false);
		AppendJson(stringBuilder, "source_capture", (enemy == null) ? string.Empty : enemy.SourceCapture, first: false);
		AppendJson(stringBuilder, "quarantine_slice", configuration.Slice, first: false);
		AppendJson(stringBuilder, "position", (enemy == null) ? string.Empty : enemy.PositionText, first: false);
		AppendJson(stringBuilder, "level", enemy?.Level ?? 0, first: false);
		AppendJson(stringBuilder, "message_type", messageType, first: false);
		AppendJson(stringBuilder, "serialized_size", size, first: false);
		AppendJson(stringBuilder, "cumulative_npc_count", value, first: false);
		AppendJson(stringBuilder, "cumulative_packet_count", value2, first: false);
		AppendJson(stringBuilder, "cumulative_byte_count", value3, first: false);
		AppendJson(stringBuilder, "elapsed_ms", (long)(utcNow - startedUtc).TotalMilliseconds, first: false);
		AppendJson(stringBuilder, "detail", detail, first: false);
		stringBuilder.Append('}');
		AppendLine(eventPath, stringBuilder.ToString());
	}

	private void WriteLedger(SubwayVisibilityDiagnosticEnemy enemy)
	{
		WriteLedger(enemy, completed: true, string.Empty);
	}

	private void WriteLedger(SubwayVisibilityDiagnosticEnemy enemy, bool completed, string failureState)
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		lock (sync)
		{
			if (!enemy.LedgerWritten)
			{
				enemy.LedgerWritten = true;
				if (!File.Exists(ledgerPath))
				{
					File.AppendAllText(ledgerPath, "SessionId,SnapshotId,PlayerIdentity,PlayfieldId,SendOrdinal,ManifestOrdinal,RuntimeIdentity,EnemyType,EnemyInstance,SourceIdentity,Name,Family,SourcePopulationGroup,SourceCapture,SelectedSlice,Position,Level,ScfuBytes,WeaponPacketCount,WeaponBytes,CharInPlayBytes,PacketCount,TotalBytes,CumulativeNpcCount,CumulativePacketCount,CumulativeBytes,SequenceCompleted,FailureState,ElapsedMs\r\n", Encoding.UTF8);
				}
				string[] array = new string[29];
				array[0] = Csv(configuration.SessionId);
				array[1] = Csv(snapshotId);
				Identity runtimeIdentity = playerIdentity;
				array[2] = Csv(((object)(Identity)(ref runtimeIdentity)).ToString());
				array[3] = playfieldId.ToString(CultureInfo.InvariantCulture);
				array[4] = enemy.SendOrdinal.ToString(CultureInfo.InvariantCulture);
				array[5] = enemy.ManifestOrdinal.ToString(CultureInfo.InvariantCulture);
				runtimeIdentity = enemy.RuntimeIdentity;
				array[6] = Csv(((object)(Identity)(ref runtimeIdentity)).ToString());
				runtimeIdentity = enemy.RuntimeIdentity;
				IdentityType type = ((Identity)(ref runtimeIdentity)).Type;
				array[7] = Csv(((object)(IdentityType)(ref type)).ToString());
				runtimeIdentity = enemy.RuntimeIdentity;
				array[8] = ((Identity)(ref runtimeIdentity)).Instance.ToString(CultureInfo.InvariantCulture);
				array[9] = Csv((enemy.SourceInstance == 0) ? string.Empty : ("SimpleChar:" + enemy.SourceInstance.ToString("X8", CultureInfo.InvariantCulture)));
				array[10] = Csv(enemy.Name);
				array[11] = Csv(enemy.Family);
				array[12] = Csv(enemy.SourceGroup);
				array[13] = Csv(enemy.SourceCapture);
				array[14] = Csv(configuration.Slice);
				array[15] = Csv(enemy.PositionText);
				array[16] = enemy.Level.ToString(CultureInfo.InvariantCulture);
				array[17] = enemy.ScfuBytes.ToString(CultureInfo.InvariantCulture);
				array[18] = enemy.WeaponCount.ToString(CultureInfo.InvariantCulture);
				array[19] = enemy.WeaponBytes.ToString(CultureInfo.InvariantCulture);
				array[20] = enemy.CharInPlayBytes.ToString(CultureInfo.InvariantCulture);
				array[21] = enemy.PacketCount.ToString(CultureInfo.InvariantCulture);
				array[22] = enemy.TotalBytes.ToString(CultureInfo.InvariantCulture);
				array[23] = enemy.CumulativeNpcCount.ToString(CultureInfo.InvariantCulture);
				array[24] = enemy.CumulativePacketCount.ToString(CultureInfo.InvariantCulture);
				array[25] = enemy.CumulativeBytes.ToString(CultureInfo.InvariantCulture);
				array[26] = (completed ? "1" : "0");
				array[27] = Csv(failureState);
				array[28] = ((long)((enemy.CompletedUtc ?? DateTime.UtcNow) - startedUtc).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
				string text = string.Join(",", array);
				File.AppendAllText(ledgerPath, text + "\r\n", Encoding.UTF8);
			}
		}
	}

	private void WriteSummary(bool completed, string status)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		lock (sync)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append('{');
			AppendJson(stringBuilder, "session_id", configuration.SessionId, first: true);
			AppendJson(stringBuilder, "snapshot_id", snapshotId, first: false);
			Identity val = playerIdentity;
			AppendJson(stringBuilder, "player_identity", ((object)(Identity)(ref val)).ToString(), first: false);
			AppendJson(stringBuilder, "playfield_id", playfieldId, first: false);
			AppendJson(stringBuilder, "total_candidate_npcs", totalCandidateNpcs, first: false);
			(spatialInterestMetrics ?? SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(0, 0, 0, 0, 0)).AppendJsonFields(stringBuilder, first: false);
			AppendJson(stringBuilder, "total_npcs_sent", totalNpcsSent, first: false);
			AppendJson(stringBuilder, "total_packet_count", totalPackets, first: false);
			AppendJson(stringBuilder, "total_serialized_bytes", totalBytes, first: false);
			AppendJson(stringBuilder, "largest_scfu_size", largestScfu, first: false);
			AppendJson(stringBuilder, "largest_per_enemy_total_size", largestEnemyTotal, first: false);
			AppendJson(stringBuilder, "first_send_timestamp", firstSendUtc.HasValue ? firstSendUtc.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty, first: false);
			AppendJson(stringBuilder, "last_send_timestamp", lastSendUtc.HasValue ? lastSendUtc.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty, first: false);
			AppendJson(stringBuilder, "total_duration_ms", (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds, first: false);
			AppendJson(stringBuilder, "last_completed_ordinal", lastCompletedOrdinal, first: false);
			AppendJson(stringBuilder, "last_completed_enemy_identity", lastCompletedIdentity, first: false);
			AppendJson(stringBuilder, "snapshot_completed", completed, first: false);
			AppendJson(stringBuilder, "snapshot_completion_status", status, first: false);
			AppendJson(stringBuilder, "selected_diagnostic_slice", configuration.Slice, first: false);
			AppendJson(stringBuilder, "expected_quarantined_row_count", configuration.ExpectedQuarantinedRowCount, first: false);
			stringBuilder.Append('}');
			File.AppendAllText(summaryPath, stringBuilder?.ToString() + "\r\n", Encoding.UTF8);
		}
	}

	private static string PacketPrefix(SubwayVisibilityDiagnosticPacketKind kind)
	{
		object result;
		switch (kind)
		{
		case SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate:
			return "SCFU";
		default:
			result = "CHAR_IN_PLAY";
			break;
		case SubwayVisibilityDiagnosticPacketKind.WeaponDefinition:
			result = "WEAPON_DEFINITION";
			break;
		}
		return (string)result;
	}

	private static void AppendLine(string path, string line)
	{
		lock (typeof(SubwayVisibilityDiagnosticSnapshot))
		{
			File.AppendAllText(path, line + "\r\n", Encoding.UTF8);
		}
	}

	private static void AppendJson(StringBuilder builder, string name, string value, bool first)
	{
		if (!first)
		{
			builder.Append(',');
		}
		builder.Append('"').Append(JsonEscape(name)).Append("\":\"")
			.Append(JsonEscape(value))
			.Append('"');
	}

	private static void AppendJson(StringBuilder builder, string name, int value, bool first)
	{
		AppendJsonNumber(builder, name, value.ToString(CultureInfo.InvariantCulture), first);
	}

	private static void AppendJson(StringBuilder builder, string name, long value, bool first)
	{
		AppendJsonNumber(builder, name, value.ToString(CultureInfo.InvariantCulture), first);
	}

	private static void AppendJson(StringBuilder builder, string name, bool value, bool first)
	{
		AppendJsonNumber(builder, name, value ? "true" : "false", first);
	}

	private static void AppendJsonNumber(StringBuilder builder, string name, string value, bool first)
	{
		if (!first)
		{
			builder.Append(',');
		}
		builder.Append('"').Append(JsonEscape(name)).Append("\":")
			.Append(value);
	}

	private static string JsonEscape(string value)
	{
		return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r")
			.Replace("\n", "\\n");
	}

	private static string Csv(string value)
	{
		return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
	}
}
