# Agent task session

- Session ID: <same as filename without extension>
- Started UTC: <ISO 8601>
- Last updated UTC: <ISO 8601>
- Task: <short goal; issue/PR reference if already available>
- Harness/version: <application and known version, or unknown>
- Model/settings: <known model ID and reasoning effort, or unknown>
- OS: <known OS, or unknown>
- Repository revision: <starting commit SHA, or unknown>; working tree: <clean/dirty/unknown>
- Guidance baseline: <starting revision; relevant skill/instruction paths and local modifications, if any>
- Capture: agent-reported; tool-call coverage unknown
- Outcome: <in-progress/completed/blocked/cancelled>
- Incidents: <number of distinct incident blocks, including recovered incidents>

## Incident 1: <short title>

- Pattern key: <stable descriptive key>
- Category: <tool/environment/navigation/instructions/unknown>
- Tool/operation and intent: <tool name, sanitized inputs, intended result; use not-applicable for non-tool obstacles>
- Observed evidence: <short actual error/output excerpt or repository-relative evidence reference>
- Impact: <blocked/delayed/reduced-confidence>; extra attempts: <count or unknown>
- Suspected cause: <hypothesis or unknown; distinguish from observed facts>
- Recovery: <what was attempted, or none>
- Verification: <verified/unverified/not-attempted>; evidence: <actual check and result, or none>
- Final state: <recovered/unresolved>; remaining limitation: <description or none>
- Improvement candidate: <optional focused suggestion, not an instruction; otherwise none>

Repeat the incident block for distinct obstacles. Remove this block for a session with zero incidents.
