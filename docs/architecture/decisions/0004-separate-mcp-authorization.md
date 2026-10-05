# ADR 0004: Give MCP clients scoped OAuth grants separate from the browser session

[Decision index](../09-architecture-decisions.md) · [Architecture index](../README.md)

- **Status:** Reconstructed / implemented
- **Documented:** 2026-10-05
- **Original approval/date:** Not established by this record

## Context and observed decision

An MCP client needs authenticated access to financial tools, explicit resource/scope validation and a revocation lifecycle. It may remain connected independently of the user's current browser session. Accepting an arbitrary user ID or the SPA session as sufficient MCP authority would not establish the intended principal/grant boundary.

[API security composition](../../../code/FinanceManager.Api/ApiConfigurationExtensions.cs) configures OpenIddict authorization-code and refresh flows with reference access tokens, token/authorization-entry validation and the `mcp` scope/resource policy. [MCP feature mapping](../../../code/FinanceManager.Api/Features/Mcp/McpFeatureExtensions.cs) requires that policy for `/mcp`, and disabled mode returns 404. [McpUserContext](../../../code/FinanceManager.Api/Features/Mcp/McpUserContext.cs) derives the owner from the authenticated subject; [financial tools](../../../code/FinanceManager.Api/Features/Mcp/Tools/FinancialAccountTools.cs) filter that owner and never accept another user's identity as authority.

Redirect URIs/permissions/PKCE requirements are reconciled with configured clients at startup; hosted signing/encryption certificates are required outside local Development/Test. The [runbook](../operations/deployment.md) explains persistent certificates, public HTTPS URLs and grant revocation. [MCP endpoint tests](../../../code/FinanceManager.Tests.Integration/Features/Mcp/McpEndpointTests.cs) cover scope, expired/revoked state, discovery and cross-user filtering.

The observed decision is **independent, scoped OAuth authorization for MCP with principal-derived ownership**, while the SPA continues using its own JWT and refresh cookie.

## Consequences and present-day assessment

Operators can revoke compromised MCP grants independently; clients get interoperable discovery and OAuth metadata. Scope/audience and repository-level ownership supplement authentication. SPA logout does not revoke an independent MCP grant.

The system now owns a second session/grant lifecycle, certificate expiry/rotation, registered-client configuration and proxy/public URL correctness. Current single active certificate-pair configuration requires a reconnect window during rotation rather than guaranteeing seamless rollover. Source/tests alone do not prove a real third-party client flow through the deployed proxy.

These are documented consequences, not reconstructed claims of original approval or historical intent.

## Alternatives and when preferable

| Alternative | Trade-off | Prefer when |
| --- | --- | --- |
| Reuse SPA JWT for MCP | Less token infrastructure; weaker separation/client onboarding and independent revocation | A strictly controlled internal client has an explicitly approved shared-token contract |
| Long-lived API keys | Easy integration but poorer scoped, user-mediated authorization and rotation ergonomics | Narrow machine-to-machine tasks require their own limited key contract, rather than general financial tools |
| External authorization server | Centralized identity/security operations; adds external availability/configuration dependency and mapping work | Organization-wide identity ownership and SSO requirements justify it |

## Review triggers

Revisit for new writable tools/scopes, confidential clients, broader identity providers, seamless key rotation or measured deployment integration problems. Extend endpoint/tool ownership tests with each newly exposed capability.
