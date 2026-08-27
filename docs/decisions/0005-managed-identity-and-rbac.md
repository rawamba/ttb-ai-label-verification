# ADR 0005: Use Managed Identity and Scoped Azure RBAC for OCR Access

- **Status:** Accepted
- **Date:** 2026-08-26

## 1. Context

The deployed prototype must authenticate to Azure Document Intelligence.

A straightforward implementation could use a Cognitive Services API key, but that would introduce a long-lived application secret that would need to be:

- generated;
- stored;
- distributed;
- rotated;
- protected in deployment configuration;
- protected from source control;
- protected from browser exposure; and
- monitored for misuse.

The Azure hosting environment already supports workload identity through App Service Managed Identity.

Azure Document Intelligence supports Microsoft Entra ID authentication and Azure role-based access control.

The application therefore does not need to depend on a static Cognitive Services key.

The design also needs to support local development without changing Application-layer verification code.

---

## 2. Decision

The deployed Azure App Service will authenticate to Azure Document Intelligence using:

```text
System-Assigned Managed Identity
```

The App Service identity is assigned the scoped Azure role:

```text
Cognitive Services User
```

at the Azure Document Intelligence resource scope.

The application uses Azure Identity credentials rather than an API key.

For local development, the Infrastructure layer uses:

```text
DefaultAzureCredential
```

so authorized developer credentials can be used without changing the OCR abstraction or verification workflow.

The overall authentication model is:

```text
Hosted Environment
Azure App Service
    |
    v
System-Assigned Managed Identity
    |
    v
Azure RBAC
    |
    v
Azure Document Intelligence
```

and locally:

```text
Developer Workstation
    |
    v
DefaultAzureCredential
    |
    v
Authorized Developer Identity
    |
    v
Azure Document Intelligence
```

---

## 3. Security Objective

The primary security objective is:

> Avoid application-managed OCR secrets when Azure workload identity can provide the required authorization.

The application therefore does not require a Cognitive Services API key in:

- source control;
- committed configuration;
- browser code;
- JavaScript;
- README setup instructions;
- deployment scripts; or
- App Service application settings.

This reduces secret-management burden and the risk of accidental credential disclosure.

---

## 4. Server-Side Trust Boundary

The browser does not communicate directly with Azure Document Intelligence.

The request path is:

```text
Evaluator / Compliance Agent
        |
        | HTTPS
        v
Azure App Service
Blazor Server / .NET 8
        |
        | Managed Identity
        v
Azure Document Intelligence
```

The browser therefore never receives:

```text
Azure OCR credentials
access tokens
API keys
service-account secrets
```

OCR authorization remains on the trusted server side.

---

## 5. Azure Resource Scope

The Managed Identity is granted the role required to invoke the Azure Document Intelligence resource.

The prototype uses:

```text
Cognitive Services User
```

scoped to the specific Document Intelligence account.

This is preferable to assigning unnecessarily broad rights at:

```text
subscription scope
resource-group scope
management-group scope
```

when only invocation of the OCR service is required.

The intent is to follow least-privilege principles appropriate to the prototype.

---

## 6. Infrastructure as Code

The identity and authorization relationship are represented in Bicep.

Relevant infrastructure responsibilities include:

```text
App Service
System-Assigned Managed Identity
Azure Document Intelligence
Cognitive Services RBAC assignment
application configuration
```

Representative modules include:

```text
infra/
  modules/
    app-service.bicep
    document-intelligence.bicep
    cognitive-services-rbac.bicep
```

Keeping the identity relationship in Infrastructure as Code makes it:

- reviewable;
- repeatable;
- version-controlled;
- less dependent on manual portal configuration.

---

## 7. Application Configuration

The application still requires non-secret provider configuration such as:

```text
DocumentIntelligence__Endpoint
DocumentIntelligence__ModelId
DocumentIntelligence__TimeoutSeconds
DocumentIntelligence__EnableFontStyling
```

These settings describe:

```text
where the service is
which model to use
how long to wait
which supported features to request
```

They are not authentication secrets.

The application should not treat an endpoint URL as sensitive merely because it identifies an Azure resource.

Authorization is enforced separately through Azure identity and RBAC.

---

## 8. Local Development

Local development uses:

```text
DefaultAzureCredential
```

This allows supported developer credentials to be discovered through the Azure Identity credential chain.

A developer must still be explicitly authorized to invoke the Document Intelligence resource.

Authentication success does not imply authorization.

The local developer identity therefore requires appropriate Azure RBAC assignment before live OCR can succeed.

---

## 9. Live Testing Boundary

Normal deterministic tests do not require Azure authorization.

Live OCR integration tests are explicitly opt-in.

This creates two distinct testing modes:

### Deterministic CI

```text
RUN_LIVE_OCR_TESTS=false
```

No live Azure OCR call is required.

### Authorized Live OCR Validation

The developer or execution identity must be authorized to invoke the Azure resource.

This separation prevents identity configuration from becoming a requirement for ordinary application-rule tests.

---

## 10. Why Managed Identity

Managed Identity provides several benefits for the hosted environment.

### No Application Secret

There is no long-lived Cognitive Services API key for the application to store.

### Credential Lifecycle Managed by Azure

The workload identity is managed by the Azure platform.

### Scoped Authorization

Azure RBAC determines which Azure resource the workload can access.

### Server-Side Authentication

The identity remains associated with the hosted workload rather than browser clients.

### Consistent Azure Identity Model

The same Azure Identity SDK model supports:

```text
developer credentials locally
managed workload identity in Azure
```

without requiring the Application layer to know which credential mechanism is active.

---

## 11. Least-Privilege Principle

The prototype's authorization model follows this principle:

> Grant the application identity only the Azure permissions required for the OCR function it performs.

The application does not require broad Azure-management rights merely to analyze a document.

The role assignment is therefore limited to the specific service and use case.

A production security review should still independently validate:

- required role;
- resource scope;
- inherited permissions;
- identity lifecycle;
- operational ownership.

---

## 12. Credential Boundary

The Infrastructure layer owns Azure credential construction.

The Application layer should not contain logic such as:

```text
API key selection
token acquisition
Managed Identity detection
Azure CLI detection
credential-chain behavior
```

The verification workflow depends on the OCR abstraction rather than the authorization mechanism used by its concrete implementation.

This keeps Azure identity concerns outside regulatory and deterministic verification logic.

---

## 13. Prototype Network Model

The evaluator-accessible prototype currently uses the public Azure Document Intelligence endpoint.

The combination is therefore:

```text
Public service endpoint
+
Microsoft Entra authentication
+
Managed Identity
+
Azure RBAC
```

Public network reachability does not imply anonymous service access.

The Azure service still requires an authorized identity.

---

## 14. Production Network Evolution

A production Treasury deployment would likely require stronger network isolation.

The architecture supports evolution toward:

```text
Azure App Service
        |
        v
VNet Integration
        |
        v
Private DNS
        |
        v
Private Endpoint
        |
        v
Azure Document Intelligence
```

Production controls could additionally include:

```text
Public network access disabled
Firewall restrictions
NSG controls
Private endpoint policies
Centralized security monitoring
Azure Policy
Resource locks
Controlled deployment identities
```

These network changes should not require modifications to deterministic verification logic.

---

## 15. Separation of Authentication and Authorization

The implementation treats authentication and authorization as separate concerns.

### Authentication

Answers:

> Who is the calling workload?

For the deployed application:

```text
System-Assigned Managed Identity
```

### Authorization

Answers:

> What is that workload allowed to do?

For the prototype:

```text
Cognitive Services User
```

at the intended Azure Document Intelligence resource scope.

This distinction is important because possessing an identity alone does not grant access to the OCR resource.

---

## 16. Impact on First-Use Latency

Managed Identity and Azure Identity initialization can contribute to first-use request latency.

Potential initialization work may include:

```text
credential selection
metadata-service communication
token acquisition
token caching
HTTP connection initialization
```

The prototype's benchmark documentation therefore avoids claiming that all first-use OCR latency originates inside Azure Document Intelligence.

Identity initialization is one of several possible contributors.

This is an operational consequence, not a reason to revert to static API keys.

---

## 17. Logging and Secret Exposure

Application logs must not contain:

```text
access tokens
authorization headers
credential material
API keys
Managed Identity tokens
```

Operational telemetry may contain non-secret information such as:

```text
correlation identifier
duration
result category
error category
endpoint host where appropriate
```

The existence of a Managed Identity does not eliminate the need for safe logging practices.

---

## 18. Consequences

### Positive

- No Cognitive Services API key is required by the deployed application.
- Long-lived OCR secrets do not need to be stored in App Service settings.
- Credentials are not exposed to browser clients.
- Azure RBAC provides a clear authorization boundary.
- Access can be scoped to the intended resource.
- Identity configuration is represented in Bicep.
- Local and hosted environments use the Azure Identity model.
- Application verification logic remains independent of credential implementation.
- The approach provides a clearer path toward production Azure security controls.

### Negative

- Correct Azure RBAC configuration is required.
- Developer identities require explicit authorization for live OCR.
- Identity/token initialization may contribute to first-use latency.
- Troubleshooting access failures requires understanding Azure Identity and RBAC.
- Production private networking still requires additional infrastructure work.
- Role assignments have deployment and propagation behavior that must be considered operationally.

---

## 19. Alternatives Considered

### 19.1 Cognitive Services API Key

Rejected for the hosted prototype.

An API key would work technically but would introduce:

```text
secret storage
secret distribution
rotation
possible source-control exposure
deployment secret management
```

when workload identity already provides a supported alternative.

---

### 19.2 API Key Stored in App Service Configuration

Rejected.

Although App Service settings can protect configuration better than source control, this would still create an application-managed static credential that is unnecessary for the prototype.

---

### 19.3 API Key Stored in Azure Key Vault

Not selected for OCR authentication.

Key Vault would improve secret storage, but the application would still be retrieving and using a static service credential.

Managed Identity allows the application to authenticate directly without introducing the OCR key at all.

Key Vault may still be appropriate for other future application secrets that cannot be replaced with workload identity.

---

### 19.4 User-Assigned Managed Identity

Not required for the current prototype.

A system-assigned identity is sufficient because the App Service currently has one direct workload-identity requirement.

A future production architecture may reconsider user-assigned identity if there is a need for:

- identity reuse across workloads;
- identity lifecycle independent of App Service;
- more complex deployment patterns;
- centralized workload-identity management.

---

### 19.5 Browser-to-OCR Authentication

Rejected.

The browser should not:

```text
receive OCR credentials
acquire service tokens
know provider authorization details
call the OCR provider directly
```

The server-side application is the appropriate trust boundary.

---

### 19.6 Broad Subscription-Level Role Assignment

Rejected.

The application only requires permission to invoke the relevant Azure AI resource.

Broad management or subscription-level access would violate the intended least-privilege model.

---

## 20. Production Security Evolution

A production implementation should extend this decision with additional controls such as:

- Microsoft Entra ID authentication for end users;
- role-based application authorization;
- conditional-access requirements where applicable;
- private endpoints;
- VNet integration;
- private DNS;
- centralized security logging;
- Azure Policy;
- privileged-identity-management practices;
- deployment-identity controls;
- identity monitoring;
- formal access reviews;
- applicable Treasury and NIST control implementation.

The prototype's Managed Identity decision provides a foundation for those controls but does not by itself represent a complete production security architecture.

---

## 21. Decision Summary

The deployed authentication path is:

```text
Compliance Agent
        |
        | HTTPS
        v
Azure App Service
        |
        | System-Assigned Managed Identity
        v
Azure RBAC
Cognitive Services User
        |
        v
Azure Document Intelligence
```

Local development uses:

```text
DefaultAzureCredential
        |
        v
Authorized Developer Identity
        |
        v
Azure Document Intelligence
```

The result is a design that avoids unnecessary static OCR credentials while keeping identity concerns outside the core verification engine.