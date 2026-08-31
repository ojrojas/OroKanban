# OroKanban

Multi-tenant Kanban project management platform built with .NET Aspire.

## Architecture

Distributed application following CQRS and Domain-Driven Design principles.

### Building Blocks

| Project | Layer | Description |
|---------|-------|-------------|
| `BuildingBlocks.Kernel.Domain` | Domain (Core) | `Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IDomainEvent`, `IBusinessRule`, `Result`/`Error`, `IRepository`, `IUnitOfWork`, `Specification<T>` composable (`And`/`Or`/`Not`) |
| `BuildingBlocks.CQRS` | Application | `ICommand`/`IQuery`/handlers, `ISender` (own dispatcher), `IPipelineBehavior` (Logging, Validation), `IDomainEventHandler` + dispatcher, lightweight validation |
| `BuildingBlocks.EventBus` | Contracts | `IntegrationEvent`, `IEventBus`, `IIntegrationEventHandler`, subscription manager |
| `BuildingBlocks.EventBus.RabbitMQ` | Infrastructure | Bus over durable topic exchange with publisher confirms, consumer `BackgroundService` with manual ack and exponential retries |
| `BuildingBlocks.Logger` | Infrastructure | Serilog structured logging configuration |
| `BuildingBlocks.ServiceDefaults` | Host | OpenTelemetry (logs/traces/metrics + OTLP), health checks (`/health`, `/alive`), HTTP resilience, `IEndpoint` for Vertical Slice, `Result → HTTP`, `GlobalExceptionHandler`, Redis token storage |

## Getting Started

```bash
aspire start
```

## Tech Stack

- **.NET Aspire 13.5** - Orchestrated cloud-native stack
- **CQRS** - Command/Query Responsibility Segregation (no MediatR)
- **Vertical Slice Architecture** - Features organized by capability
- **RabbitMQ** - Message broker for event bus (no MassTransit)
- **Redis** - Distributed caching and token storage
- **Serilog** - Structured logging
- **OpenTelemetry** - Observability (logs, traces, metrics)

## Design Decisions

- **No MediatR**: `Sender` resolves handlers from DI with cached generic wrappers; behaviors are open generics registered in order.
- **No MassTransit**: `RabbitMqEventBus` publishes to a durable *topic* exchange with publisher confirms; each service consumes from its own queue with manual ack and configurable QoS. *At-least-once* delivery — integration event handlers must be idempotent.
- **No AutoMapper**: manual mapping in handlers (in vertical slices the mapping is local to each feature).
- **Domain events vs integration events**: domain events are in-process and dispatched within `SaveChanges`; integration events cross services and go through the transactional outbox.

## Project Structure

```
src/
  BuildingBlocks/
    BuildingBlocks.Kernel.Domain/   # Domain primitives
    BuildingBlocks.CQRS/            # CQRS primitives
    BuildingBlocks.EventBus/        # Event bus abstractions
    BuildingBlocks.EventBus.RabbitMQ/
    BuildingBlocks.Logger/
    BuildingBlocks.ServiceDefaults/
OroKanban.AppHost/
  AppHost.cs                        # Aspire orchestration
draft/
  libraries/buildingblocks.md       # BuildingBlocks docs
  oroidentityserver-specification.md
```

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.400)
