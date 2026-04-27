namespace Crucible.Sample.Orders.Domain.Dtos;

public sealed record OrderDto(string CustomerId, decimal Amount, string Currency);
