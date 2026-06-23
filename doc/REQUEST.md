## Abstract

This specification defines a minimal, parameterless cyclic trigger contract for ecosystems in which services, agents, or workloads are not expected to run permanent background processes.

Instead of keeping internal loops, timers, or continuously active workers alive, a service exposes a well-known cyclic trigger endpoint and allows external infrastructure to periodically reactivate it.

The cyclic trigger endpoint defines a domain-neutral activation mechanism. It does not describe business intent, workload type, schedule ownership, or execution parameters. It only exposes a standardized way for external infrastructure to offer a recurring execution opportunity to an otherwise passive system.

The trigger request does not describe what should be done, how much work should be performed, or when the next execution is required. These decisions remain fully internal to the receiving service. This parameterless design is intentional: it prevents external callers from becoming part of the business logic, avoids distributed state coupling, and keeps the trigger contract stable across large heterogeneous systems.

The model reverses the traditional background-worker assumption. Services may stay idle, cheap, and operationally simple, while a central control mechanism cyclically reawakens them according to ecosystem-level policies.

This enables cost-efficient, externally governed execution for large service landscapes, especially where background processing is restricted, undesirable, or operationally expensive.

------

## Trigger Endpoint Specification

A compliant implementation SHALL expose a trigger endpoint at the following well-known location:

```text
/.well-known/cyclic-trigger/go
```

The well-known URI suffix is `cyclic-trigger`.

The additional path component `go` identifies the trigger operation and MUST be lowercase.

### Request

The endpoint MUST accept HTTP POST requests.

The request body MUST contain an empty JSON object:

```json
{}
```

No request parameters, query string parameters, route parameters, headers, or payload attributes are defined by this specification.

The trigger contract is intentionally parameterless. A trigger merely signals an execution opportunity. All decisions regarding scheduling, throttling, execution frequency, workload selection, and internal state remain the responsibility of the receiving service.

### Successful Response

A successful trigger request MUST return HTTP status code `200 OK`.

The response body MUST contain an empty JSON object:

```json
{}
```

Example:

```http
POST /.well-known/cyclic-trigger/go HTTP/1.1
Content-Type: application/json

{}
```

Response:

```http
HTTP/1.1 200 OK
Content-Type: application/json

{}
```

### Error Response

Implementations SHOULD avoid exposing infrastructure-specific exception details through HTTP status codes.

Errors MUST therefore also return HTTP status code `200 OK`.

In error situations, the response body SHALL contain a JSON object with a `fault` property:

```json
{
  "fault": "A human-readable error description. Sensitive information MUST NOT be exposed here."
}
```

Example:

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "fault": "The trigger receiver is currently unavailable."
}
```

### Rationale

The protocol intentionally distinguishes success and failure through the response payload rather than through HTTP status codes.

This allows orchestration systems, service catalogs, schedulers, and trigger relays to process responses uniformly without requiring transport-level error handling semantics.

The endpoint is designed as a minimal activation contract rather than a remote procedure call. The caller requests activation; the receiver remains fully responsible for deciding whether work is executed.

------

## Well-Known URI Enrollment Request

URI suffix: `cyclic-trigger`

Change controller: `<name of responsible party or organization>`

Specification document(s): `<URI of this specification document>`

Status: `provisional`

Related information: None.

### Registration Rationale

The `cyclic-trigger` well-known URI defines a domain-neutral, cross-cutting activation mechanism for services, agents, and workloads that should not maintain permanent background processes.

The registered suffix identifies a capability of an origin: the ability to receive externally governed cyclic activation signals.

The operation itself is exposed through the additional path component:

```text
/.well-known/cyclic-trigger/go
```

The endpoint is intentionally parameterless. It does not expose domain-specific commands, scheduling instructions, workload identifiers, or execution parameters. Its only purpose is to provide a stable, interoperable activation signal that allows external infrastructure to periodically offer an execution opportunity to a passive receiver.

The registration avoids generic names such as `trigger` or `activation` and uses the more specific suffix `cyclic-trigger` to describe the intended protocol-level concern.