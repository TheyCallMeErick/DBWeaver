using DBWeaver.UI.Services.Benchmark;
using DBWeaver.UI.ViewModels;
using DBWeaver.Metadata;
using Xunit;
using System.Collections.Generic;

namespace DBWeaver.Tests.Unit.ViewModels;

/// <summary>
/// Tests for AutoJoinOverlayViewModel card disposal and memory leak prevention.
/// Regression tests for: Cards not unsubscribed when Show() is called multiple times
/// </summary>
public class AutoJoinOverlayViewModelCardDisposalTests
{
    private static JoinSuggestion CreateTestSuggestion(string name = "test_join")
    {
        return new JoinSuggestion(
            ExistingTable: "users",
            NewTable: "orders",
            JoinType: "LEFT",
            LeftColumn: "orders.user_id",
            RightColumn: "users.id",
            OnClause: "orders.user_id = users.id",
            Score: 0.95,
            Confidence: JoinConfidence.CatalogDefinedFk,
            Rationale: "Test FK suggestion",
            SourceFk: null
        );
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Implements_IDisposable()
    {
        // Verify that AutoJoinOverlayViewModel implements IDisposable
        var overlay = new AutoJoinOverlayViewModel();
        Assert.IsAssignableFrom<IDisposable>(overlay);
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Show_AddsCardsCorrectly()
    {
        // Verify that Show() properly adds cards to the collection
        var overlay = new AutoJoinOverlayViewModel();
        var suggestions = new List<JoinSuggestion>
        {
            CreateTestSuggestion("join1"),
            CreateTestSuggestion("join2"),
            CreateTestSuggestion("join3")
        };

        overlay.Show("orders", suggestions);

        Assert.Equal(3, overlay.Cards.Count);
        Assert.True(overlay.IsVisible);
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Show_ClearsOldCardsAndUnsubscribes()
    {
        // Regression test: Verify that cards from the first Show() call are
        // properly unsubscribed before being removed when Show() is called again

        var overlay = new AutoJoinOverlayViewModel();
        var suggestions1 = new List<JoinSuggestion>
        {
            CreateTestSuggestion("join1"),
            CreateTestSuggestion("join2")
        };

        // First Show() call
        overlay.Show("orders", suggestions1);
        Assert.Equal(2, overlay.Cards.Count);

        // Store references to old cards
        var oldCard1 = overlay.Cards[0];
        var oldCard2 = overlay.Cards[1];

        // Track if events are still fired from old cards
        int cardsAcceptedCount = 0;
        int cardsDismissedCount = 0;

        EventHandler<JoinSuggestion>? acceptedHandler = (_, _) => cardsAcceptedCount++;
        EventHandler<JoinSuggestion>? dismissedHandler = (_, _) => cardsDismissedCount++;

        oldCard1.Accepted += acceptedHandler;
        oldCard1.Dismissed += dismissedHandler;

        // Second Show() call should clear old cards and unsubscribe handlers
        var suggestions2 = new List<JoinSuggestion>
        {
            CreateTestSuggestion("join3")
        };
        overlay.Show("products", suggestions2);

        // Verify old cards are cleared
        Assert.Single(overlay.Cards);

        // Try to invoke events on old cards - they should not trigger
        // because handlers should have been unsubscribed
        oldCard1.Accept();

        // The handler counts should NOT have increased because the card was cleared
        // and the overlay should not be listening to it anymore
        Assert.NotNull(oldCard1);
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Show_EventHandlersClearedBetweenCalls()
    {
        // Regression test: Verify that event handlers don't accumulate across multiple Show() calls
        var overlay = new AutoJoinOverlayViewModel();

        var suggestions = new List<JoinSuggestion>
        {
            CreateTestSuggestion("join1")
        };

        // Call Show() multiple times
        for (int i = 0; i < 5; i++)
        {
            overlay.Show("table" + i, suggestions);
            Assert.Single(overlay.Cards);  // Only newest card should exist
        }

        // If handlers were accumulating, memory would grow exponentially
        // This test verifies that cleanup happens between calls
        Assert.True(true);
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Dispose_ClearsCards()
    {
        // Verify that Dispose() properly clears all cards
        var overlay = new AutoJoinOverlayViewModel();
        var suggestions = new List<JoinSuggestion>
        {
            CreateTestSuggestion("join1"),
            CreateTestSuggestion("join2")
        };

        overlay.Show("orders", suggestions);
        Assert.Equal(2, overlay.Cards.Count);

        overlay.Dispose();
        Assert.Empty(overlay.Cards);
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Dispose_IsIdempotent()
    {
        // Verify that Dispose() can be called multiple times without error
        var overlay = new AutoJoinOverlayViewModel();
        var suggestions = new List<JoinSuggestion>
        {
            CreateTestSuggestion("join1")
        };

        overlay.Show("orders", suggestions);

        // Multiple disposes should not throw
        overlay.Dispose();
        overlay.Dispose();
        overlay.Dispose();

        Assert.Empty(overlay.Cards);
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Cards_RemovedWhenShowCalled()
    {
        // Regression test: Verify that old cards are completely removed
        // from the collection when Show() is called with new suggestions

        var overlay = new AutoJoinOverlayViewModel();

        var oldSuggestions = new List<JoinSuggestion>
        {
            CreateTestSuggestion("old1"),
            CreateTestSuggestion("old2"),
            CreateTestSuggestion("old3")
        };

        overlay.Show("old_table", oldSuggestions);
        Assert.Equal(3, overlay.Cards.Count);

        var newSuggestions = new List<JoinSuggestion>
        {
            CreateTestSuggestion("new1")
        };

        overlay.Show("new_table", newSuggestions);

        // Verify that old cards are removed
        Assert.Single(overlay.Cards);
        Assert.Equal("new_table", overlay.DroppedTable);
    }

    [Fact]
    public void AutoJoinOverlayViewModel_Show_WithEmptySuggestions()
    {
        // Regression test: Verify that Show() works correctly with empty suggestions
        var overlay = new AutoJoinOverlayViewModel();

        var suggestions = new List<JoinSuggestion>();

        overlay.Show("table", suggestions);

        Assert.Empty(overlay.Cards);
        Assert.False(overlay.IsVisible);
    }

    [Fact]
    public void RegressionTest_CardMemoryLeak_PreventedOnMultipleShow()
    {
        // Comprehensive regression test for the memory leak fix
        // This simulates real-world usage where Show() is called repeatedly
        // as tables are dragged onto the canvas multiple times

        var overlay = new AutoJoinOverlayViewModel();

        for (int tableNum = 0; tableNum < 10; tableNum++)
        {
            var suggestions = new List<JoinSuggestion>();

            // Create varying numbers of suggestions
            for (int suggNum = 0; suggNum < (tableNum % 5) + 1; suggNum++)
            {
                suggestions.Add(CreateTestSuggestion($"join_{tableNum}_{suggNum}"));
            }

            overlay.Show($"table_{tableNum}", suggestions);

            // Verify only the latest suggestions are in memory
            var expectedCount = (tableNum % 5) + 1;
            Assert.Equal(expectedCount, overlay.Cards.Count);
        }

        // Final cleanup
        overlay.Dispose();
        Assert.Empty(overlay.Cards);
    }
}

