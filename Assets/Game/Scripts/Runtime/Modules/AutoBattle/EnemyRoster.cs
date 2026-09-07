using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;

namespace Game
{
    /// <summary>单次战斗的敌人集合、实例 ID 与确定性选敌边界。</summary>
    internal sealed class EnemyRoster
    {
        private long _nextEnemyId = 1;
        private readonly List<BattleEnemy> _items = new();
        private readonly ReadOnlyCollection<BattleEnemy> _itemsView;

        internal EnemyRoster()
        {
            _itemsView = _items.AsReadOnly();
        }

        internal IReadOnlyList<BattleEnemy> Items => _itemsView;
        public int Count => _items.Count;


        internal BattleEnemy Spawn(
            EnemyType type,
            float maxHealth,
            float pathPosition,
            float attackIntervalSeconds)
        {
            var enemy = new BattleEnemy(
                _nextEnemyId++,
                type,
                maxHealth,
                pathPosition,
                attackIntervalSeconds);
            _items.Add(enemy);
            return enemy;
        }

        internal BattleEnemy FindLiving(long runtimeId)
        {
            return _items.Find(enemy =>
                enemy.RuntimeId == runtimeId && enemy.Health > 0f);
        }

        internal void RemoveDefeated()
        {
            _items.RemoveAll(enemy => enemy.Health <= 0f);
        }

        internal void Tick(float deltaTime)
        {
            foreach (var enemy in _items)
            {
                enemy.Tick(deltaTime);
            }
        }

        internal BattleEnemy FindNearestToBook()
        {
            BattleEnemy nearest = null;
            foreach (var enemy in _items)
            {
                if (enemy.Health <= 0f ||
                    nearest != null && CompareByPathThenId(enemy, nearest) >= 0)
                {
                    continue;
                }

                nearest = enemy;
            }

            return nearest;
        }

        internal IReadOnlyList<long> SelectChainTargets(
            BattleEnemy primaryTarget,
            int targetCount,
            float chainRange)
        {
            var selected = new List<long>(targetCount) { primaryTarget.RuntimeId };
            var current = primaryTarget;

            while (selected.Count < targetCount)
            {
                var next = FindNearestUnselected(
                    current.PathPosition,
                    chainRange,
                    selected);
                if (next == null)
                {
                    break;
                }

                selected.Add(next.RuntimeId);
                current = next;
            }

            return selected;
        }

        internal IReadOnlyList<long> SelectAreaTargets(float center, float range)
        {
            var targets = new List<BattleEnemy>();
            foreach (var enemy in _items)
            {
                if (enemy.Health > 0f && Math.Abs(enemy.PathPosition - center) <= range)
                {
                    targets.Add(enemy);
                }
            }

            targets.Sort(CompareByPathThenId);
            var targetIds = new List<long>(targets.Count);
            foreach (var target in targets)
            {
                targetIds.Add(target.RuntimeId);
            }

            return targetIds;
        }

        private BattleEnemy FindNearestUnselected(
            float origin,
            float range,
            IReadOnlyList<long> selected)
        {
            BattleEnemy nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var enemy in _items)
            {
                if (enemy.Health <= 0f || Contains(selected, enemy.RuntimeId))
                {
                    continue;
                }

                var distance = Math.Abs(enemy.PathPosition - origin);
                if (distance > range)
                {
                    continue;
                }

                if (nearest == null ||
                    distance < nearestDistance ||
                    distance == nearestDistance && CompareByPathThenId(enemy, nearest) < 0)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static int CompareByPathThenId(BattleEnemy left, BattleEnemy right)
        {
            var pathComparison = left.PathPosition.CompareTo(right.PathPosition);
            return pathComparison != 0
                ? pathComparison
                : left.RuntimeId.CompareTo(right.RuntimeId);
        }

        private static bool Contains(IReadOnlyList<long> values, long value)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
