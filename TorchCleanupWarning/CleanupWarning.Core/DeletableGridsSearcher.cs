using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using Sandbox.Game.Entities;

namespace CleanupWarning.Core
{
    public class DeletableGridsSearcher
    {
        public struct GridGroup
        {
            public string GroupName { get; set; }
            public ISet<long> Owners { get; set; }
            public IEnumerable<MyCubeGrid> Grids { get; set; }

            public bool IsOwnedBy(long userId) => Owners.Contains(userId);
        }

        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly Regex NamePattern = new Regex(@"^(Static|Large|Small)\s(Grid|Ship).*$");
        readonly List<GridGroup> _groups;
        readonly HashSet<string> _ignoredGroupNames;
        readonly HashSet<long> _ignoredPlayers;

        public DeletableGridsSearcher()
        {
            _groups = new List<GridGroup>();
            _ignoredGroupNames = new HashSet<string>();
            _ignoredPlayers = new HashSet<long>();
        }

        public void CollectGrids()
        {
            lock (this)
            {
                _groups.Clear();

                foreach (var group in MyCubeGridGroups.Static.Logical.Groups)
                {
                    var grids = group.Nodes.Select(n => n.NodeData).ToArray();
                    var groupName = grids.First().DisplayName;
                    var owners = grids.SelectMany(g => g.BigOwners.Concat(g.SmallOwners));

                    var gridGroup = new GridGroup
                    {
                        GroupName = groupName,
                        Owners = new HashSet<long>(owners),
                        Grids = grids,
                    };

                    _groups.Add(gridGroup);

                    Log.Trace($"group: [{string.Join(", ", gridGroup.Owners)}] {groupName}");
                }
            }
        }

        public IEnumerable<GridGroup> SearchDeletableGrids(long userId)
        {
            if (_ignoredPlayers.Contains(userId))
            {
                return Enumerable.Empty<GridGroup>();
            }

            return _groups
                .Where(g => !_ignoredGroupNames.Contains(g.GroupName))
                .Where(g => g.IsOwnedBy(userId))
                .Where(g => IsGridGroupDeletable(g.Grids))
                .ToArray();
        }

        // Returns false if the specified group "may" not exist in the game
        public bool IgnoreGroup(string groupName)
        {
            lock (this)
            {
                _ignoredGroupNames.Add(groupName);
                return _groups.Any(g => g.GroupName == groupName);
            }
        }

        public void IgnorePlayer(long userId)
        {
            lock (this)
            {
                _ignoredPlayers.Add(userId);
            }
        }

        public void UnignorePlayer(long userId)
        {
            lock (this)
            {
                _ignoredPlayers.Remove(userId);
            }
        }

        static bool IsGridGroupDeletable(IEnumerable<MyCubeGrid> group)
        {
            // Unowned grids should be deleted
            var owners = group.SelectMany(g => g.BigOwners.Concat(g.SmallOwners));
            if (!owners.Any()) return true;

            // Unnamed grids should be deleted
            if (group.All(g => IsUnnamed(g))) return true;

            return false;
        }

        static bool IsUnnamed(MyCubeGrid grid)
        {
            return NamePattern.IsMatch(grid.DisplayName);
        }
    }
}