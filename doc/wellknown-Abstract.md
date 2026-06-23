

## Abstract

This specification defines a minimal, parameterless trigger contract for ecosystems in which services, agents, or workloads are not expected to run permanent background processes.

Instead of keeping internal loops, timers, or continuously active workers alive, a service exposes a well-known `Trigger` route and allows external infrastructure to periodically reactivate it.

The trigger request does not describe what should be done, how much work should be performed, or when the next execution is required. These decisions remain fully internal to the receiving service. This parameterless design is intentional: it prevents external callers from becoming part of the business logic, avoids distributed state coupling, and keeps the trigger contract stable across large heterogeneous systems.

The model reverses the traditional background-worker assumption. Services may stay idle, cheap, and operationally simple, while a central control mechanism cyclically reawakens them according to ecosystem-level policies.

This enables cost-efficient, externally governed execution for large service landscapes, especially where background processing is restricted, undesirable, or operationally expensive.



## Trigger Endpoint Specification

A compliant implementation SHALL expose a trigger endpoint at the following well-known location:

```text
/.well-known/cyclic-trigger/GO
```

The endpoint name is case-sensitive and the sub-route 'GO' MUST be in uppercase.

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
POST /.well-known/cyclic-trigger/GO HTTP/1.1
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

In error situations, the response body SHALL contain a JSON object with an `fault` property:

```json
{
  "fault": "A human readable error description (expose no sensisve information here)."
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