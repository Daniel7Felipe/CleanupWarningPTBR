using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodenameGenerator.Lite;
using NLog;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API.Managers;
using VRageMath;

namespace CleanupWarning.Core
{
    public class CleanupWarning
    {
        public interface IConfig
        {
            float Interval { get; }
        }

        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly IChatManagerServer _chatManager;
        readonly DeletableGridsSearcher _gridsSearcher;

        public CleanupWarning(IConfig config, IChatManagerServer chatManager, DeletableGridsSearcher gridsSearcher)
        {
            _config = config;
            _chatManager = chatManager;
            _gridsSearcher = gridsSearcher;
        }

        public async Task RunWarningCycles(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Log.Info($"Running warning (interval: {_config.Interval}secs)");

                _gridsSearcher.CollectGrids();

                foreach (var onlinePlayer in MySession.Static.Players.GetOnlinePlayers())
                {
                    WarnUnnamedGridsIfAny(onlinePlayer);
                }

                await Task.Delay(TimeSpan.FromSeconds(_config.Interval), cancellationToken);
            }
        }

        public void ForceCollect()
        {
            _gridsSearcher.CollectGrids();
        }

        public void WarnUnnamedGridsIfAny(MyPlayer player)
        {
            var messageBuilder = new StringBuilder();

            var userId = player.Identity.IdentityId;
            var steamId = player.Id.SteamId;

            Log.Trace($"{userId} processing");

            var groups = _gridsSearcher.SearchDeletableGrids(userId);
            if (!groups.Any()) return;

            var groupNames = groups.Select(g => g.GroupName).ToArray();

            Log.Trace($"{userId} {string.Join(", ", groupNames)}");

            messageBuilder.AppendLine();
            messageBuilder.AppendLine("NAME YOUR GRIDS ASAP!");
            messageBuilder.AppendLine($"{string.Join("\n", groupNames.Select(g => $"> '{g}'"))}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"> !{CleanupWarningCommandModule.Consts.CommandBase} {CleanupWarningCommandModule.Consts.Help}");

            SendMessage(steamId, Color.Red, messageBuilder.ToString());
        }

        public bool IgnoreGroup(string groupName)
        {
            return _gridsSearcher.IgnoreGroup(groupName);
        }

        public void IgnoreUser(long userId)
        {
            _gridsSearcher.IgnorePlayer(userId);
        }

        public void UnignoreUser(long userId)
        {
            _gridsSearcher.UnignorePlayer(userId);
        }

        public void RenameUnnamedGrids(MyPlayer player)
        {
            var messageBuilder = new StringBuilder();

            var playerId = player.Identity.IdentityId;
            var steamId = player.Id.SteamId;

            var faction = MySession.Static.Factions.TryGetPlayerFaction(playerId);
            if (faction == null)
            {
                SendMessage(steamId, Color.Red, "Join or make a faction first!");
                return;
            }

            var factionTag = faction.Tag;
            var playerName = player.DisplayName;

            var renames = new List<(string OriginalName, string NewName)>();

            var groups = _gridsSearcher.SearchDeletableGrids(playerId);

            Parallel.ForEach(groups, async group =>
            {
                var newBaseName = await GenerateName();
                var newName = $"{factionTag} {playerName} {newBaseName}";

                var topGrid = group.Grids.First();
                if (topGrid.Closed) return; // deleted already

                topGrid.ChangeDisplayNameRequest(newName);

                var originalName = group.GroupName;
                renames.Add((originalName, newName));

                Log.Trace($"renamed '{originalName}' -> '{newName}");
            });

            messageBuilder.AppendLine("Renamed your unnamed grids!");
            foreach (var (originalName, newName) in renames)
            {
                messageBuilder.AppendLine($"| '{originalName}' -> '{newName}'");
            }

            SendMessage(steamId, default, messageBuilder.ToString());
        }

        public void DeleteUnnamedGrids(MyPlayer player)
        {
            var playerId = player.Identity.IdentityId;
            var steamId = player.Id.SteamId;

            // force-collect grids in the entire game world
            _gridsSearcher.CollectGrids();

            var groups = _gridsSearcher.SearchDeletableGrids(playerId);
            var groupNames = groups.Select(g => g.GroupName);

            foreach (var group in groups)
            foreach (var grid in group.Grids)
            {
                if (grid.Closed) continue; // deleted already
                grid.Close();
            }

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Deleted your unnamed grids!");

            foreach (var groupName in groupNames)
            {
                messageBuilder.AppendLine($"> '{groupName}'");
            }

            SendMessage(steamId, default, messageBuilder.ToString());
        }

        public void ShowUnnamedGrids(MyPlayer player)
        {
            var playerId = player.Identity.IdentityId;
            var steamId = player.Id.SteamId;

            var gpsCollection = (MyGpsCollection) MyAPIGateway.Session.GPS;
            var groups = _gridsSearcher.SearchDeletableGrids(playerId);
            foreach (var group in groups)
            {
                var topGrid = group.Grids.First();
                if (topGrid.Closed) continue; // deleted already

                var entityId = topGrid.EntityId;
                var name = topGrid.DisplayName;
                var position = topGrid.PositionComp.GetPosition();
                var gps = new MyGps
                {
                    Coords = position,
                    Description = "Unnamed grid can be automatically cleaned up anytime!",
                    DisplayName = name,
                    Name = name,
                    ShowOnHud = true,
                };

                gpsCollection.SendAddGps(playerId, ref gps, entityId);
            }

            SendMessage(steamId, default, "Added GPS of unnamed grids in your HUD!");
        }

        static Task<string> GenerateName()
        {
            var generator = new Generator();
            generator.SetParts(WordBank.Adjectives, WordBank.Nouns);
            generator.Casing = Casing.PascalCase;
            return generator.GenerateAsync();
        }

        void SendMessage(ulong steamId, Color color, string message)
        {
            _chatManager?.SendMessageAsOther(null, message, color, steamId);
        }
    }
}