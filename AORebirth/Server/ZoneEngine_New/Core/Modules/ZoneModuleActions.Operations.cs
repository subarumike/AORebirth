namespace ZoneEngine_New.Core.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using AORebirth.Core.GameData;
    using AORebirth.Modules.World;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// The named operations (module API 1.5). Each registers its name, target kind, parameters, the capability it needs
    /// (its own name) and where it runs: player and NPC operations on the owning playfield tick, account operations on a
    /// worker thread. Every operation goes through the engine's normal paths; none writes packets of its own.
    /// </summary>
    internal sealed partial class ZoneModuleActions
    {
        /// <summary>The issuer recorded on a sanction a module writes when the caller is not known (no capability gate).</summary>
        internal const string UnknownModuleIssuer = "module";

        sealed record Operation(OperationDescriptor Descriptor, Func<OperationTarget, OperationArgs, string, Task<OperationResult>> Run);

        static OperationParameter Required(string name, OperationValueType type, string summary) => new(name, type, true, summary);

        static OperationParameter Optional(string name, OperationValueType type, string summary) => new(name, type, false, summary);

        public IReadOnlyList<OperationDescriptor> Operations => _operations.Values.Select(x => x.Descriptor).ToArray();

        public Task<OperationResult> Invoke(string operation, OperationTarget target, OperationArgs args)
            => Invoke(operation, target, args, UnknownModuleIssuer);

        /// <summary>
        /// <see cref="Invoke(string, OperationTarget, OperationArgs)"/> on behalf of <paramref name="issuer"/>
        /// (<c>module:Id</c>), which account sanctions record. The capability gate passes it, so a module cannot set it.
        /// </summary>
        internal Task<OperationResult> Invoke(string operation, OperationTarget target, OperationArgs args, string issuer)
        {
            args ??= OperationArgs.Empty;
            if (string.IsNullOrEmpty(operation) || !_operations.TryGetValue(operation, out Operation? registered))
                return Task.FromResult(OperationResult.Fail(OperationCodes.UnknownOperation, "No operation named '" + operation + "'."));

            OperationDescriptor descriptor = registered.Descriptor;
            if (target.Kind != descriptor.Target)
                return Task.FromResult(OperationResult.Fail(OperationCodes.BadTarget,
                    descriptor.Name + " takes a " + descriptor.Target + " target, not " + target.Kind + "."));

            foreach (OperationParameter parameter in descriptor.Parameters)
            {
                bool present = parameter.Type switch
                {
                    OperationValueType.Int => args.TryGetInt(parameter.Name, out _),
                    OperationValueType.Float => args.TryGetFloat(parameter.Name, out _),
                    OperationValueType.Bool => args.TryGetBool(parameter.Name, out _),
                    _ => args.TryGetString(parameter.Name, out _)
                };
                if (!present && (parameter.Required || args.Has(parameter.Name)))
                    return Task.FromResult(OperationResult.Fail(OperationCodes.BadArgs,
                        descriptor.Name + " needs '" + parameter.Name + "' as " + parameter.Type + "."));
            }

            return registered.Run(target, args, issuer);
        }

        public Task<IReadOnlyList<AccountSanctionInfo>> AccountSanctions(string account, int limit = 20)
        {
            if (_accounts == null || string.IsNullOrWhiteSpace(account))
                return Task.FromResult<IReadOnlyList<AccountSanctionInfo>>(Array.Empty<AccountSanctionInfo>());

            return ReadSanctions(_accounts(), account, Math.Clamp(limit, 1, 500));
        }

        static async Task<IReadOnlyList<AccountSanctionInfo>> ReadSanctions(
            ZoneEngine_New.Core.Accounts.AccountSanctionService accounts, string account, int limit)
        {
            IReadOnlyList<ZoneEngine_New.Core.Accounts.AccountSanction> rows = await accounts.History(account, limit).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            return rows.Select(x => new AccountSanctionInfo(x.Id, x.Account, x.IssuedAtUtc, x.EndsAtUtc, x.IssuedBy, x.Reason,
                x.LiftedAtUtc, x.LiftedBy, x.LiftReason, x.IsActive(now))).ToArray();
        }

        Dictionary<string, Operation> BuildOperations()
        {
            var operations = new Dictionary<string, Operation>(StringComparer.OrdinalIgnoreCase);
            void Add(string name, TargetKind target, string summary, OperationParameter[] parameters,
                Func<OperationTarget, OperationArgs, Task<OperationResult>> run)
                => operations.Add(name, new Operation(new OperationDescriptor(name, target, parameters, name, summary),
                    (on, args, _) => run(on, args)));
            void AddAccount(string name, string summary, OperationParameter[] parameters,
                Func<OperationTarget, OperationArgs, string, Task<OperationResult>> run)
                => operations.Add(name, new Operation(new OperationDescriptor(name, TargetKind.Account, parameters, name, summary), run));

            Add(OperationNames.NpcRefresh, TargetKind.Npc,
                "Sends the NPC's spawn extras again to everyone who sees it.", [],
                async (target, _) => Done(await RefreshNpc(target.PlayfieldId, target.Id).ConfigureAwait(false),
                    "The NPC is not spawned on that playfield."));
            Add(OperationNames.NpcRespawn, TargetKind.Npc,
                "Removes the NPC as if it died (no corpse, loot or XP) and respawns its spawn point now.", [],
                async (target, _) => Done(await RespawnNpc(target.PlayfieldId, target.Id).ConfigureAwait(false),
                    "The NPC is not spawned there, or has no spawn point that respawns."));
            Add(OperationNames.PlayerMessage, TargetKind.Character,
                "A line of system text in the player's chat window.",
                [Required("text", OperationValueType.String, "The text.")],
                async (target, args) => Done(await SendSystemMessage(target.Id, args.GetString("text")).ConfigureAwait(false),
                    "The player is not in play here."));
            Add(OperationNames.PlayerFeedback, TargetKind.Character,
                "A client feedback message by id.",
                [Required("id", OperationValueType.Int, "The feedback id.")],
                async (target, args) => Done(await SendFeedback(target.Id, args.TryGetInt("id", out int id) ? id : 0)
                    .ConfigureAwait(false), "The player is not in play here."));
            Add(OperationNames.PlayerTeleport, TargetKind.Character,
                "Moves the player: within its playfield, or to another one (on this instance or another).",
                [
                    Required("playfield", OperationValueType.Int, "The destination playfield id."),
                    Required("x", OperationValueType.Float, "East-west."),
                    Required("y", OperationValueType.Float, "Height."),
                    Required("z", OperationValueType.Float, "North-south.")
                ],
                Teleport);
            Add(OperationNames.PlayerKick, TargetKind.Character,
                "Logs the player out as a client logout does, with the normal logout save.",
                [Optional("reason", OperationValueType.String, "For the log.")],
                Kick);
            if (_accounts != null)
            {
                AddAccount(OperationNames.AccountBan,
                    "Bans the account permanently (a sanction row) and removes its characters in play.",
                    [Optional("reason", OperationValueType.String, "Kept on the sanction and told to the player.")],
                    (target, args, issuer) => Sanction(target, args, issuer, null));
                AddAccount(OperationNames.AccountSuspend,
                    "Suspends the account for a time (a sanction row) and removes its characters in play. It ends by itself.",
                    [
                        Required("seconds", OperationValueType.Int, "How long: 60 seconds to ten years."),
                        Optional("reason", OperationValueType.String, "Kept on the sanction and told to the player.")
                    ],
                    (target, args, issuer) =>
                    {
                        args.TryGetInt("seconds", out int seconds);
                        return Sanction(target, args, issuer, seconds);
                    });
                AddAccount(OperationNames.AccountUnban,
                    "Lifts every active suspension and ban of the account, including a pre-1.8 login.Flags ban.",
                    [Optional("reason", OperationValueType.String, "Kept on the lifted sanctions.")],
                    Unban);
            }
            if (_shutdown != null)
            {
                Add(OperationNames.ServerShutdown, TargetKind.None,
                    "Shuts this zone instance down gracefully (players saved) after a countdown every online player is told about. Scheduling again replaces it.",
                    [
                        Required("seconds", OperationValueType.Int, "Seconds until the shutdown, 1 to 86400."),
                        Optional("message", OperationValueType.String, "Shown to players after each countdown line."),
                        Optional("reason", OperationValueType.String, "For the log.")
                    ],
                    ScheduleShutdown);
                Add(OperationNames.ServerShutdownAbort, TargetKind.None,
                    "Cancels a scheduled shutdown and tells online players.",
                    [Optional("reason", OperationValueType.String, "For the log.")],
                    AbortShutdown);
            }

            return operations;
        }

        Task<OperationResult> ScheduleShutdown(OperationTarget target, OperationArgs args)
        {
            args.TryGetInt("seconds", out int seconds);
            if (seconds < 1 || seconds > ZoneEngine_New.Core.Lifecycle.ShutdownScheduler.MaxSeconds)
                return Task.FromResult(OperationResult.Fail(OperationCodes.BadArgs, string.Format(CultureInfo.InvariantCulture,
                    "seconds must be 1 to {0}.", ZoneEngine_New.Core.Lifecycle.ShutdownScheduler.MaxSeconds)));

            // The scheduler counts down on its own timer and announces from there, so this never waits on a tick.
            _shutdown!().Schedule(seconds, ShutdownRequester(args), args.GetString("message"));
            return Task.FromResult(OperationResult.Success(string.Format(CultureInfo.InvariantCulture,
                "Shutdown in {0}s.", seconds)));
        }

        Task<OperationResult> AbortShutdown(OperationTarget target, OperationArgs args)
            => Task.FromResult(_shutdown!().Abort(ShutdownRequester(args))
                ? OperationResult.Success()
                : OperationResult.Fail(OperationCodes.Failed, "No shutdown is scheduled."));

        static string ShutdownRequester(OperationArgs args)
        {
            string reason = args.GetString("reason");
            return reason.Length == 0 ? "module operation" : "module operation (" + reason + ")";
        }

        static OperationResult Done(bool done, string notHere)
            => done ? OperationResult.Success() : OperationResult.Fail(OperationCodes.NotHere, notHere);

        Task<OperationResult> Teleport(OperationTarget target, OperationArgs args)
        {
            args.TryGetInt("playfield", out int playfieldId);
            args.TryGetFloat("x", out float x);
            args.TryGetFloat("y", out float y);
            args.TryGetFloat("z", out float z);
            if (playfieldId <= 0 || !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
                return Task.FromResult(OperationResult.Fail(OperationCodes.BadArgs, "A playfield id and finite coordinates are needed."));
            if (_gameDataRoot != null
                && !Directory.Exists(Path.Combine(_gameDataRoot, GameDataPaths.PlayfieldRelativeDirectory(playfieldId))))
                return Task.FromResult(OperationResult.Fail(OperationCodes.BadArgs, "Unknown playfield " + playfieldId + "."));

            var landing = new Vector3(x, y, z);
            return OnPlayer(target.Id, player =>
            {
                if (player.Playfield is not Playfield current)
                    return OperationResult.Fail(OperationCodes.NotHere, "The player is not on a playfield.");

                // Same playfield: a soft teleport, as .tp does.
                if (current.Identity.Instance == playfieldId)
                {
                    player.TeleportWithinPlayfield(landing);
                    return OperationResult.Success();
                }

                if (player.Session == null)
                    return OperationResult.Fail(OperationCodes.NotHere, "The player has no connection.");

                // Another playfield: no exit-proxy return to come back through, as a GM summon from another instance.
                player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
                player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
                bool moving = _manager.Value.WithPlayfield(playfieldId, player,
                    destination => player.Session?.TransferToPlayfield(destination, landing),
                    new ZoneEngine_New.Core.World.RemoteArrival(landing, Kind: AORebirth.World.Contracts.V1.ArrivalKind.ArrivalTeleport));
                return moving
                    ? OperationResult.Success()
                    : OperationResult.Fail(OperationCodes.Failed, "The player is already changing playfield.");
            }, OperationResult.Fail(OperationCodes.NotHere, "The player is not in play here."));
        }

        Task<OperationResult> Kick(OperationTarget target, OperationArgs args)
        {
            string reason = args.GetString("reason");
            return OnPlayer(target.Id, player =>
            {
                if (player.Playfield is not Playfield playfield)
                    return OperationResult.Fail(OperationCodes.NotHere, "The player is not on a playfield.");

                _logger?.Info(string.Format(CultureInfo.InvariantCulture,
                    "Module operation player.kick character={0} reason={1}", player.Identity.Instance, reason));
                playfield.GetRequiredService<SpawnService>().LogoutPlayer(player);
                return OperationResult.Success();
            }, OperationResult.Fail(OperationCodes.NotHere, "The player is not in play here."));
        }

        /// <summary>account.ban (<paramref name="seconds"/> null) and account.suspend.</summary>
        async Task<OperationResult> Sanction(OperationTarget target, OperationArgs args, string issuer, long? seconds)
        {
            string account = target.Account?.Trim() ?? string.Empty;
            if (account.Length == 0)
                return OperationResult.Fail(OperationCodes.BadArgs, "An account name is needed.");
            if (seconds is < ZoneEngine_New.Core.Accounts.AccountSanctionService.MinSuspensionSeconds
                or > ZoneEngine_New.Core.Accounts.AccountSanctionService.MaxSuspensionSeconds)
                return OperationResult.Fail(OperationCodes.BadArgs, "seconds must be 60 seconds to ten years.");

            ZoneEngine_New.Core.Accounts.SanctionIssueResult result;
            try
            {
                result = await _accounts!().Issue(account, seconds, issuer, args.GetString("reason")).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.Error(exception, "Module account sanction failed for account " + account + ".");
                return OperationResult.Fail(OperationCodes.Failed, "The sanction could not be recorded.");
            }

            return result.Ok
                ? OperationResult.Success(string.Format(CultureInfo.InvariantCulture, "Sanction #{0} until {1}; {2} character(s) removed here.",
                    result.Sanction!.Id, ZoneEngine_New.Core.Accounts.AccountSanctionService.Describe(result.Sanction.EndsAtUtc), result.Removed))
                : OperationResult.Fail(OperationCodes.Failed, result.Error);
        }

        async Task<OperationResult> Unban(OperationTarget target, OperationArgs args, string issuer)
        {
            string account = target.Account?.Trim() ?? string.Empty;
            if (account.Length == 0)
                return OperationResult.Fail(OperationCodes.BadArgs, "An account name is needed.");

            ZoneEngine_New.Core.Accounts.SanctionLift lift;
            try
            {
                lift = await _accounts!().Lift(account, issuer, args.GetString("reason")).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.Error(exception, "Module account unban failed for account " + account + ".");
                return OperationResult.Fail(OperationCodes.Failed, "The sanctions could not be lifted.");
            }

            if (!lift.AccountFound)
                return OperationResult.Fail(OperationCodes.Failed, "No account named '" + account + "'.");
            return lift.Lifted == 0 && !lift.LegacyFlagsCleared
                ? OperationResult.Fail(OperationCodes.Failed, "The account has no active suspension or ban.")
                : OperationResult.Success(string.Format(CultureInfo.InvariantCulture, "{0} sanction(s) lifted.", lift.Lifted));
        }
    }
}
