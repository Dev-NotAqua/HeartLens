namespace Hearthlens.Core.Battlegrounds;

/// <summary>One minion as it stands when the first attack of a combat starts.</summary>
public sealed record SimMinion(
    int Attack,
    int Health,
    bool Taunt,
    bool DivineShield,
    bool Poisonous,
    bool Venomous,
    bool Reborn,
    int AttacksPerTurn,
    bool Stealth,
    int RebornAttack,
    bool HasUnsimulatedEffect);

/// <summary>Estimated outcome of a combat. <see cref="Unsimulated"/> counts minions whose card effects are ignored.</summary>
public sealed record CombatOdds(double Win, double Tie, double Loss, int Unsimulated)
{
    public bool Approximate => Unsimulated > 0;
}

/// <summary>
/// Monte Carlo Battlegrounds combat: attack order, random targets, taunt, divine shield, poisonous, venomous,
/// reborn, windfury and stealth. Deathrattles, auras and triggered effects are not modelled, which the result
/// reports through <see cref="CombatOdds.Unsimulated"/>. Start-of-combat effects have already resolved when the
/// boards are captured (at the first attack), so they are reflected in the stats.
/// </summary>
public static class CombatSimulator
{
    private const int MaxAttacks = 400;

    /// <param name="firstAttackerIsOurs">Who attacks first if known (read from the first attack), else null.</param>
    public static CombatOdds Run(IReadOnlyList<SimMinion> ours, IReadOnlyList<SimMinion> theirs, bool? firstAttackerIsOurs, int runs = 4000)
    {
        var unsimulated = ours.Count(m => m.HasUnsimulatedEffect) + theirs.Count(m => m.HasUnsimulatedEffect);
        if (ours.Count == 0 || theirs.Count == 0)
        {
            var win = ours.Count > 0 ? 1.0 : 0;
            var loss = theirs.Count > 0 ? 1.0 : 0;
            return new CombatOdds(win, 1 - win - loss, loss, unsimulated);
        }

        // Stable seed: the same boards always show the same numbers.
        var random = new Random(Seed(ours, theirs));
        int wins = 0, losses = 0;
        for (var i = 0; i < runs; i++)
        {
            switch (Fight(ours, theirs, firstAttackerIsOurs, random))
            {
                case > 0: wins++; break;
                case < 0: losses++; break;
            }
        }

        return new CombatOdds(wins / (double)runs, (runs - wins - losses) / (double)runs, losses / (double)runs, unsimulated);
    }

    /// <summary>+1 we win, -1 we lose, 0 tie.</summary>
    private static int Fight(IReadOnlyList<SimMinion> oursStart, IReadOnlyList<SimMinion> theirsStart, bool? firstOurs, Random random)
    {
        var sides = new[] { oursStart.Select(m => new Unit(m)).ToList(), theirsStart.Select(m => new Unit(m)).ToList() };
        var next = new int[2];
        var side = firstOurs switch
        {
            true => 0,
            false => 1,
            // The side with more minions attacks first; a coin flip on equal counts.
            null => sides[0].Count > sides[1].Count ? 0 : sides[1].Count > sides[0].Count ? 1 : random.Next(2),
        };

        for (var step = 0; step < MaxAttacks && sides[0].Count > 0 && sides[1].Count > 0; step++)
        {
            var own = sides[side];
            var foe = sides[1 - side];
            var index = NextAttacker(own, next[side]);
            if (index < 0)
            {
                // This side cannot attack; if neither can, the fight stalls into a tie.
                if (NextAttacker(foe, next[1 - side]) < 0)
                {
                    return 0;
                }

                side = 1 - side;
                continue;
            }

            var attacker = own[index];
            for (var swing = 0; swing < attacker.AttacksPerTurn && attacker.Health > 0 && foe.Count > 0; swing++)
            {
                Attack(attacker, own, foe, ref next[1 - side], random);
            }

            // Continue from the slot right of the attacker; a reborn copy takes the attacker's slot.
            var position = own.IndexOf(attacker);
            var resume = position >= 0 ? position + 1
                : index < own.Count && own[index].Origin == attacker ? index + 1
                : index;
            next[side] = own.Count == 0 ? 0 : resume % own.Count;
            side = 1 - side;
        }

        return sides[0].Count > 0 && sides[1].Count == 0 ? 1
            : sides[1].Count > 0 && sides[0].Count == 0 ? -1
            : 0;
    }

    private static int NextAttacker(List<Unit> side, int from)
    {
        for (var i = 0; i < side.Count; i++)
        {
            var index = (from + i) % side.Count;
            if (side[index].Attack > 0)
            {
                return index;
            }
        }

        return -1;
    }

    /// <param name="foeNext">Index of the defending side's next attacker; kept on the same minion when one to its left dies.</param>
    private static void Attack(Unit attacker, List<Unit> own, List<Unit> foe, ref int foeNext, Random random)
    {
        var defender = PickTarget(foe, random);
        attacker.Stealth = false;
        var attackPower = attacker.Attack;
        Hit(defender, attackPower, attacker);
        Hit(attacker, defender.Attack, defender);
        if (RemoveDead(foe, defender) is { Removed: >= 0 } removed && !removed.Reborn && removed.Removed < foeNext)
        {
            foeNext--;
        }

        RemoveDead(own, attacker);
    }

    private static Unit PickTarget(List<Unit> foe, Random random)
    {
        var visible = foe.Where(u => !u.Stealth).ToList();
        if (visible.Count == 0)
        {
            visible = foe;
        }

        var taunts = visible.Where(u => u.Taunt).ToList();
        var pool = taunts.Count > 0 ? taunts : visible;
        return pool[random.Next(pool.Count)];
    }

    private static void Hit(Unit target, int damage, Unit source)
    {
        if (damage <= 0)
        {
            return;
        }

        if (target.DivineShield)
        {
            target.DivineShield = false;
            return;
        }

        target.Health -= damage;
        if (source.Poisonous || source.Venomous)
        {
            target.Health = Math.Min(target.Health, 0);
            source.Venomous = false;
        }
    }

    /// <summary>Removes a dead unit; returns its former index (-1 if alive) and whether a reborn copy took its slot.</summary>
    private static (int Removed, bool Reborn) RemoveDead(List<Unit> side, Unit unit)
    {
        if (unit.Health > 0)
        {
            return (-1, false);
        }

        var index = side.IndexOf(unit);
        if (index < 0)
        {
            return (-1, false);
        }

        side.RemoveAt(index);
        if (unit.Reborn && side.Count < 7)
        {
            side.Insert(index, unit.RebornCopy());
            return (index, true);
        }

        return (index, false);
    }

    private static int Seed(IReadOnlyList<SimMinion> ours, IReadOnlyList<SimMinion> theirs)
    {
        var hash = new HashCode();
        foreach (var m in ours) hash.Add(m);
        hash.Add(-1);
        foreach (var m in theirs) hash.Add(m);
        return hash.ToHashCode();
    }

    private sealed class Unit
    {
        public Unit(SimMinion m)
        {
            Attack = m.Attack;
            Health = m.Health;
            Taunt = m.Taunt;
            DivineShield = m.DivineShield;
            Poisonous = m.Poisonous;
            Venomous = m.Venomous;
            Reborn = m.Reborn;
            AttacksPerTurn = Math.Max(1, m.AttacksPerTurn);
            Stealth = m.Stealth;
            RebornAttack = m.RebornAttack;
        }

        public int Attack { get; }
        public int Health { get; set; }
        public bool Taunt { get; }
        public bool DivineShield { get; set; }
        public bool Poisonous { get; }
        public bool Venomous { get; set; }
        public bool Reborn { get; }
        public int AttacksPerTurn { get; }
        public bool Stealth { get; set; }
        public int RebornAttack { get; }

        /// <summary>The minion this one was reborn from, or null.</summary>
        public Unit? Origin { get; private init; }

        /// <summary>Reborn brings the minion back with its base attack and 1 health, without reborn.</summary>
        public Unit RebornCopy() => new(new SimMinion(RebornAttack, 1, Taunt, false, Poisonous, false, false, AttacksPerTurn, false, RebornAttack, false))
        {
            Origin = this,
        };
    }
}
