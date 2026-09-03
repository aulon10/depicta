using Depicta.Api.Domain.Enums;
using Depicta.Api.Domain.StateMachine;
using Xunit;

namespace Depicta.Tests.Unit;

/// <summary>
/// The state machine is the product's core safety guarantee: an image can never reach
/// <see cref="MediaItemState.Applied"/> (a write to the live site) without passing through
/// <see cref="MediaItemState.Approved"/> — i.e. without the owner clicking Approve.
/// </summary>
public class MediaItemStateMachineTests
{
    [Theory]
    [InlineData(MediaItemState.Unprocessed, MediaItemState.Processing)]
    [InlineData(MediaItemState.Processing, MediaItemState.PendingReview)]
    [InlineData(MediaItemState.Processing, MediaItemState.Failed)]
    [InlineData(MediaItemState.PendingReview, MediaItemState.Approved)]
    [InlineData(MediaItemState.PendingReview, MediaItemState.Rejected)]
    [InlineData(MediaItemState.PendingReview, MediaItemState.Unprocessed)] // redo
    [InlineData(MediaItemState.Approved, MediaItemState.Applied)]
    [InlineData(MediaItemState.Applied, MediaItemState.Reverted)]
    [InlineData(MediaItemState.Rejected, MediaItemState.Unprocessed)]      // regenerate
    [InlineData(MediaItemState.Reverted, MediaItemState.Approved)]         // re-apply
    [InlineData(MediaItemState.Failed, MediaItemState.Unprocessed)]        // retry
    public void Allows_legal_transitions(MediaItemState from, MediaItemState to)
        => Assert.True(MediaItemStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(MediaItemState.Unprocessed, MediaItemState.Applied)]
    [InlineData(MediaItemState.PendingReview, MediaItemState.Applied)]     // must approve first
    [InlineData(MediaItemState.Processing, MediaItemState.Applied)]
    [InlineData(MediaItemState.Applied, MediaItemState.Approved)]
    [InlineData(MediaItemState.Reverted, MediaItemState.Applied)]          // must re-approve first
    [InlineData(MediaItemState.Unprocessed, MediaItemState.PendingReview)]
    public void Rejects_illegal_transitions(MediaItemState from, MediaItemState to)
        => Assert.False(MediaItemStateMachine.CanTransition(from, to));

    [Fact]
    public void Applied_is_reachable_only_from_Approved()
    {
        foreach (var from in Enum.GetValues<MediaItemState>())
        {
            var expected = from == MediaItemState.Approved;
            Assert.Equal(expected, MediaItemStateMachine.CanTransition(from, MediaItemState.Applied));
        }
    }

    [Fact]
    public void EnsureTransition_throws_on_illegal_move()
        => Assert.Throws<InvalidStateTransitionException>(
            () => MediaItemStateMachine.EnsureTransition(MediaItemState.PendingReview, MediaItemState.Applied));

    [Fact]
    public void EnsureTransition_allows_legal_move()
    {
        var ex = Record.Exception(
            () => MediaItemStateMachine.EnsureTransition(MediaItemState.Approved, MediaItemState.Applied));
        Assert.Null(ex);
    }
}
