-  You have read and understood the requirements for registration.
-  You have checked the registry and found no current value that meets your needs.
-  Your specification reference URL is stable; ideally, managed by a widely-recognised standards development organisation (e.g., published as an RFC). Otherwise, please give additional information.

If so, please enter the details of the well-known URI below:

- URI suffix: `cyclic-trigger`
- Change controller: SmartStandards Community / T.Korn
- Specification document(s): https://github.com/SmartStandards/.well-known.cyclic-trigger

Any additional information (this will not be included in the registry)?

The `cyclic-trigger` well-known URI defines a domain-neutral activation capability of an origin.

The specification provides a standardized mechanism for externally governed cyclic activation of services, agents, and workloads that should not maintain permanent background processes. It is intended as an infrastructure-level protocol rather than an application-specific API.

A participating origin exposes the protocol-defined activation operation at:

```
/.well-known/cyclic-trigger/go
```

The operation accepts an empty JSON object via HTTP POST and intentionally carries no parameters. A trigger merely offers an execution opportunity. Decisions regarding scheduling, throttling, workload selection, execution frequency, and internal state remain entirely within the receiving system.

The protocol is designed for ecosystems that prefer passive, externally reactivated workloads over continuously running background workers, timers, or internal scheduling loops.

The specification is publicly available and maintained by the SmartStandards open-source community.