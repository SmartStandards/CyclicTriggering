# Cyclic Trigger Receiver for .NET

## Overview

This package provides the `ICyclicTriggerReceiver` abstraction together with a ready-to-use .NET implementation, specifically designed for ASP.NET Core hosted services.

It is an implementation of the SmartStandards Cyclic Trigger specification:

[Cyclic Trigger Specification](https://github.com/SmartStandards/.well-known.cyclic-trigger?utm_source=chatgpt.com)

The goal is to support background activities that need to be executed repeatedly, but should **not** run as permanently looping worker threads.

Instead of maintaining long-running internal loops, a service exposes a standardized cyclic trigger endpoint and allows external infrastructure to periodically reactivate it.

```csharp
public interface ICyclicTriggerReceiver {
    
  void Go(); 

}
```

An external component is responsible for invoking this trigger periodically.

------

## Motivation

Traditional background workers often rely on dedicated threads, timers, or endless execution loops. While this approach can work, it becomes problematic in environments such as IIS where application pools may be recycled at any time.

Common issues include:

- Application pool recycles terminating worker threads unexpectedly.
- Background loops being silently stopped during application restarts.
- Zombie processes caused by improperly detached long-running threads.
- Difficult operational visibility and centralized scheduling.
- Scheduling logic being tightly coupled to application lifetime.

This library addresses these problems by replacing internal scheduling with a standardized cyclic activation mechanism.

------

## Concept

A service publishes itself as an `ICyclicTriggerReceiver`.

The published contract communicates a simple expectation:

> "Offer me execution opportunities regularly."

The service itself contains the actual business logic but does not decide when it should run.

A centralized scheduler or worker process periodically invokes the receiver, typically via the standardized Cyclic Trigger endpoint:

```text
/.well-known/cyclic-trigger/go
```

(see also: ['.well-Known' URIs](https://www.iana.org/assignments/well-known-uris/well-known-uris.xhtml) following [RFC8615](https://datatracker.ietf.org/doc/html/rfc8615))

This allows scheduling responsibilities to be moved out of the application process and consolidated into a dedicated infrastructure component.

When combined with service announcement, endpoint catalogs, or discovery mechanisms, a central worker can automatically discover participating services and periodically invoke them without requiring service-specific scheduling configuration.

------

## Execution Model

The implementation is designed for recurring work items rather than continuously running processes.

Typical examples include:

- Synchronization jobs
- Cache refreshes
- Cleanup tasks
- Polling external systems
- Status aggregation
- Scheduled maintenance operations

The registered worker method is executed only when a cyclic activation request is received.

------

## Cyclic Trigger Semantics

The cyclic trigger mechanism intentionally does **not** provide hard real-time guarantees.

For example, a service may be configured with a desired activation interval of 10 seconds.

The implementation follows these principles:

### Minimum Interval Protection

If activation requests arrive too frequently, they are ignored.

Example:

- Desired interval: 10 seconds
- Activation received after 3 seconds
- Activation is ignored

This prevents excessive execution caused by duplicate schedulers, network retries, or misconfiguration.

### Single Execution Protection

If the previously activated execution is still running, additional activation requests are ignored.

This guarantees that only one worker execution is active at a time.

Example:

- Worker execution started
- Worker still running
- Additional activation received
- Activation is ignored

### Best-Effort Scheduling

The implementation aims for a target frequency rather than exact timing.

For example, a configuration of "every 10 seconds" should be interpreted as:

> Execute approximately every 10 seconds whenever possible.

Actual execution intervals may vary depending on:

- Trigger delivery timing
- Worker execution duration
- System load
- Application restarts
- Network latency

------

## Why Cyclic Activation?

The SmartStandards Cyclic Trigger specification defines a domain-neutral activation mechanism.

It does not describe business intent, workload type, schedule ownership, or execution parameters. It only exposes a standardized way for external infrastructure to offer a recurring execution opportunity to an otherwise passive system.

External cyclic activation provides several advantages over internal loops:

- IIS application pool recycles are no longer responsible for maintaining scheduling state.
- Services become stateless regarding execution timing.
- Scheduling can be centralized and monitored.
- Multiple services can be coordinated from a single scheduler.
- Operational visibility is significantly improved.
- Long-running worker threads are avoided entirely.
- Services may remain dormant between activations.

The result is a more robust and infrastructure-friendly execution model for recurring background work in ASP.NET Core applications.

------

# Comparison with BackgroundService and IHostedService

The primary goal of this library is not to replace `BackgroundService` or `IHostedService` in general.

Both approaches are perfectly valid when an application owns its scheduling logic and is expected to remain continuously active.

However, in environments where services are frequently recycled, restarted, scaled, or hosted behind IIS application pools, internal scheduling can become problematic.

Typical implementations rely on:

- Endless execution loops
- Timers
- Delayed retries
- Long-lived worker threads

As a result, scheduling logic becomes tightly coupled to application lifetime.

The Cyclic Trigger approach intentionally follows a different model:

| Aspect                        | BackgroundService / IHostedService | Cyclic Trigger Receiver |
| ----------------------------- | ---------------------------------- | ----------------------- |
| Scheduling location           | Inside application                 | External scheduler      |
| Endless worker loop           | Usually required                   | Not required            |
| Long-lived threads            | Common                             | Avoided                 |
| IIS recycle resilience        | Limited                            | High                    |
| Centralized scheduling        | No                                 | Yes                     |
| Service discovery integration | Limited                            | Natural fit             |
| Operational visibility        | Medium                             | High                    |
| Dormant services              | No                                 | Yes                     |

The library is especially useful in service-oriented environments where a central component already maintains a catalog of available service endpoints.

Instead of embedding scheduling logic into every individual service, a dedicated scheduler can periodically activate all registered receivers through a common mechanism.

This allows scheduling policies to be modified, monitored, and scaled independently from the services performing the actual work.

------

# Design Philosophy

The implementation intentionally does not provide strict timing guarantees.

A configured interval should be interpreted as a desired execution frequency rather than a contractual execution schedule.

For example:

```text
Typical interval: 10 seconds
```

does not mean:

```text
Execute exactly every 10 seconds.
```

Instead, the implementation follows two simple rules:

1. If the previous execution is still running, the activation is ignored.
2. If the activation arrives too early, the activation is ignored.

This behaviour intentionally favors robustness over precision.

The goal is not to guarantee execution timing, but to guarantee that work is not executed excessively, concurrently, or under unnecessary load.

In practice this results in a highly stable execution model for synchronization jobs, maintenance tasks, polling operations, cache refreshes, and other recurring business activities.

------

# Typical Deployment Scenario

```text
+----------------------+
| Central Scheduler    |
+----------+-----------+
           |
           v
+----------------------+
| Endpoint Catalog     |
| (Service Discovery)  |
+----------+-----------+
           |
           +--------------------+
           |                    |
           v                    v

+----------------+    +----------------+
| Service A      |    | Service B      |
| go()           |    | go()           |
+----------------+    +----------------+
```

Services announce their cyclic trigger endpoints.

A centralized scheduler discovers these endpoints and periodically invokes:

```text
/.well-known/cyclic-trigger/go
```

The services themselves remain completely unaware of scheduling infrastructure and only implement the business logic that should be executed when an activation arrives.