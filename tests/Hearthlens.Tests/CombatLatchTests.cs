using Hearthlens.Core.Power;
using Hearthlens.Core.Tracking;
using Xunit;

namespace Hearthlens.Tests;

/// <summary>
/// Regression tests for the overlay getting stuck on "Combat" while the player is in the tavern: some client
/// builds never log the closing BOARD_VISUAL_STATE line, so the combat latch must expire.
/// </summary>
public sealed class CombatLatchTests
{
    [Fact]
    public void Stale_combat_screen_latch_is_released()
    {
        var game = new GameState
        {
            CombatOnScreen = true,
            CombatOnScreenSince = Environment.TickCount64 - 200_000,
        };

        Assert.True(BattlegroundsTracker.CombatLatchStale(game));
        Assert.False(game.CombatOnScreen);
        Assert.Equal(0, game.CombatOnScreenSince);
    }

    [Fact]
    public void Fresh_combat_screen_latch_is_kept()
    {
        var game = new GameState
        {
            CombatOnScreen = true,
            CombatOnScreenSince = Environment.TickCount64,
        };

        Assert.False(BattlegroundsTracker.CombatLatchStale(game));
        Assert.True(game.CombatOnScreen);
    }

    [Fact]
    public void Latch_without_timestamp_is_kept()
    {
        var game = new GameState { CombatOnScreen = true };

        Assert.False(BattlegroundsTracker.CombatLatchStale(game));
        Assert.True(game.CombatOnScreen);
    }
}
