namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Playfield;

    public sealed class SpawnCommand : IGmCommand
    {
        private readonly IGameData _gameData;

        public SpawnCommand(IGameData gameData)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            _gameData = gameData;
        }

        public string Name => "spawn";

        public int RequiredGmLevel => 1;

        public string Usage => ".spawn <Hash> <Level>";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 2)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            string hash = context.Args[0];
            if (!int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                || level < 1)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            Playfield? playfield = context.Player.Playfield;
            if (playfield == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Not on a playfield.");
                return;
            }

            SpawnService spawn = playfield.GetRequiredService<SpawnService>();
            if (!_gameData.CanResolveMobHash(hash))
            {
                SpawnStatic(context, spawn, hash, level);
                return;
            }

            NpcCharacter npc = spawn.Spawn(
                hash,
                context.Player.Position,
                context.Player.Rotation,
                level,
                SpawnSource.Command);

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawned {0} id={1} level={2}",
                    npc.Name,
                    npc.Identity.Instance,
                    npc.Stats.GetOrZero(CharacterStat.Level)));
        }

        static void SpawnStatic(GmCommandContext context, SpawnService spawn, string hash, int level)
        {
            if (!spawn.CanSpawnStatic(hash))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(CultureInfo.InvariantCulture, "Unknown mob or item hash: {0}", hash));
                return;
            }

            StaticDynel dynel = spawn.SpawnStatic(
                hash,
                context.Player.Position,
                context.Player.Rotation,
                level,
                SpawnSource.Command);

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawned static {0} id={1} template={2} ql={3}",
                    dynel.Template.Name,
                    dynel.Identity.Instance,
                    dynel.Template.Id,
                    dynel.Template.Quality));
        }
    }
}
