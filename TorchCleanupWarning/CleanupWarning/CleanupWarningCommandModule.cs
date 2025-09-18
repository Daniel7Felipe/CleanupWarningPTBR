using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.General;
using VRage.Game.ModAPI;

namespace CleanupWarning
{
    [Category(Consts.CommandBase)]
    public class CleanupWarningCommandModule : CommandModule
    {
        public static class Consts
        {
            public const string CommandBase = "cw";
            public const string Collect = "collect";
            public const string Help = "help";
            public const string Send = "send";
            public const string Ignore = "ignore";
            public const string AutoName = "autoname";
            public const string Delete = "delete";
            public const string Show = "show";
        }

        static readonly Logger Log = LogManager.GetCurrentClassLogger();

        CleanupWarningPlugin Plugin => (CleanupWarningPlugin) Context.Plugin;
        Core.CleanupWarning Core => Plugin.Core;
        MyPlayer Player => (MyPlayer) Context.Player;

        [Command(Consts.Collect, "Force-collects unnamed grids in the game world")]
        [Permission(MyPromoteLevel.Admin)]
        public void ForceCollect()
        {
            Core.ForceCollect();
        }

        [Command(Consts.Help, "Shows list of commands")]
        [Permission(MyPromoteLevel.None)]
        public void Help()
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"> !{Consts.CommandBase} {Consts.Show}");
            messageBuilder.AppendLine($"> !{Consts.CommandBase} {Consts.AutoName}");
            messageBuilder.AppendLine($"> !{Consts.CommandBase} {Consts.Delete}");

            Context.Respond(messageBuilder.ToString());
        }

        [Command(Consts.Ignore, "Exclude given group from the warning search")]
        [Permission(MyPromoteLevel.None)]
        public void Ignore(string arg)
        {
            var userId = Player.Identity.IdentityId;

            Log.Info($"{nameof(Ignore)}({userId}, {arg})");

            if (arg.ToLower() == "all")
            {
                Core.IgnoreUser(userId);
                Context.Respond("You will no longer receive this warning!");
                return;
            }

            if (arg.ToLower() == "none")
            {
                Core.UnignoreUser(userId);
                Context.Respond("You will receive cleanup warning!");
                return;
            }

            if (Core.IgnoreGroup(arg))
            {
                Context.Respond($"Ignored the grid '{arg}'!");
            }
            else
            {
                Context.Respond($"Grid '{arg}' not found");
            }

            Core.WarnUnnamedGridsIfAny(Player);
        }

        [Command(Consts.Send, "Sends a warning to given player.")]
        [Permission(MyPromoteLevel.None)]
        public void SendWarning()
        {
            Core.WarnUnnamedGridsIfAny(Player);
        }

        [Command(Consts.AutoName, "Generates a new name for all unnamed grids of given player.")]
        [Permission(MyPromoteLevel.None)]
        public void AutoRenameUnnamedGrids()
        {
            Task.Factory
                .StartNew(() => Core.RenameUnnamedGrids(Player))
                .Forget(Log);
        }

        [Command(Consts.Delete, "Deletes unnamed grids of given player's posession.")]
        [Permission(MyPromoteLevel.None)]
        public void DeleteUnnamedGrids()
        {
            Task.Factory
                .StartNew(() => Core.DeleteUnnamedGrids(Player))
                .Forget(Log);
        }

        [Command(Consts.Show, "Shows GPS of unnamed grids on given player's HUD.")]
        [Permission(MyPromoteLevel.None)]
        public void ShowUnnamedGrids()
        {
            Task.Factory
                .StartNew(() => Core.ShowUnnamedGrids(Player))
                .Forget(Log);
        }
    }
}