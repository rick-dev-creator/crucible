namespace Crucible.Generators.Tests.Fixtures;

internal static class ApprovalWorkflowInput
{
    public const string Source = @"
using Crucible.Domain.Aggregates;
using Crucible.Domain.Attributes;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;
using Crucible.Domain.Results;

namespace Sample;

public readonly record struct OrderId(System.Guid Value) : IAggregateId<OrderId>
{
    public static OrderId New() => new(System.Guid.NewGuid());
    public static OrderId From(System.Guid v) => new(v);
}

public sealed record OrderDto(string CustomerId);
public sealed record OrderCreated(OrderId Id) : DomainEvent;
public sealed record OrderApproved(OrderId Id, string Approver) : DomainEvent;
public sealed record OrderRejected(OrderId Id, string Reason) : DomainEvent;
public sealed record OrderPlaced(OrderId Id) : DomainEvent;
public sealed record OrderCancelled(OrderId Id) : DomainEvent;

[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    private Order() { }

    [Step(Order = 1, Entry = true)]
    public Result<OrderCreated> Create(OrderDto dto)
    {
        Id = OrderId.New();
        Raise(new OrderCreated(Id));
        return new OrderCreated(Id);
    }

    [Step(Order = 2, AllowedAfter = new[] { nameof(Create) })]
    public Result<OrderApproved> Approve(string approver)
    {
        Raise(new OrderApproved(Id, approver));
        return new OrderApproved(Id, approver);
    }

    [Step(Order = 2, AllowedAfter = new[] { nameof(Create) })]
    public Result<OrderRejected> Reject(string reason)
    {
        Raise(new OrderRejected(Id, reason));
        return new OrderRejected(Id, reason);
    }

    [Step(Order = 3, AllowedAfter = new[] { nameof(Approve) })]
    public Result<OrderPlaced> Place()
    {
        Raise(new OrderPlaced(Id));
        return new OrderPlaced(Id);
    }

    [Step(Order = 3, AllowedAfter = new[] { nameof(Reject) })]
    public Result<OrderCancelled> Cancel()
    {
        Raise(new OrderCancelled(Id));
        return new OrderCancelled(Id);
    }
}
";
}
