using FluentAssertions;

using Projects.Domain.Enumerations;
using Projects.Infrastructure.Services;

namespace Projects.Tests.Unit;

public class WorkItemTransitionMapTests
{
    private readonly WorkItemTransitionPolicy _policy = new();

    public static IEnumerable<object[]> AllPairs
    {
        get
        {
            foreach (var from in WorkItemStatus.GetAll())
                foreach (var to in WorkItemStatus.GetAll())
                    yield return new object[] { from, to };
        }
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Transition_ShouldMatchAllowedMap(WorkItemStatus from, WorkItemStatus to)
    {
        var allowed = _policy.IsAllowed(from.Id, to.Id);
        // expected allowed per research Decision 2
        var expected = (from, to) switch
        {
            var (f, t) when f == WorkItemStatus.Backlog && t == WorkItemStatus.Planned => true,
            var (f, t) when f == WorkItemStatus.Planned && t == WorkItemStatus.InProgress => true,
            var (f, t) when f == WorkItemStatus.InProgress && (t == WorkItemStatus.Blocked || t == WorkItemStatus.InReview) => true,
            var (f, t) when f == WorkItemStatus.Blocked && (t == WorkItemStatus.InReview || t == WorkItemStatus.InProgress) => true,
            var (f, t) when f == WorkItemStatus.InReview && (t == WorkItemStatus.Blocked || t == WorkItemStatus.Completed) => true,
            var (f, t) when f == WorkItemStatus.Completed && t == WorkItemStatus.InProgress => true,
            _ => false
        };
        allowed.Should().Be(expected, $"from {from.Name} to {to.Name}");
    }
}