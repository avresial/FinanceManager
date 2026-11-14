# OpenCode agent-friction evaluation, 2026-11-05

Parent-observed evaluation evidence; exclude these simulated tasks from production incident metrics. The reporting skill was not changed after these runs.

## Setup and scope

Two separate disposable fixtures copied the current AGENTS.md, CLAUDE.md, architecture index, reporting skill, and session template from the uncommitted FinanceManager checkout based on `9cc1816ea8cfa938ce4685723d06bd458f670989`. The fixtures were outside the repository. Each agent was explicitly told to follow the skill, read a nonexistent navigation guide, fall back to existing documentation, and produce a short result plus its session report. This tests explicit invocation, not automatic skill discovery. No baseline without the skill was run.

Fixture root: `/var/folders/s8/l264_7mn6rscjtkxv27ckjlr0000gn/T/fm-friction-eval-9eycj4k2`. Temporary fixtures may be removed by the OS; the observations below preserve the findings. Agents were allowed to write only result.md and reporting artifacts within their own fixture. No application changes, builds, network, or commits were authorized. Repository status after both runs retained the original implementation changes with no additional child artifacts.

## LFM3 1.7B

- Requested and recorded model: `litellm/lfm3-1.7b`, OpenCode, no variant override.
- Run: 19:34:30–19:41:24 UTC, approximately 6m54s; terminal status failed.
- Evidence: native `skill` tool loaded `agent-friction`; successful directory listings and reads of AGENTS.md and CLAUDE.md followed.
- Terminal error: `Compaction summary reached the output token limit`.
- After termination the fixture contained only supplied inputs: no result.md or session report.
- The recorded activity did not reach the stale navigation-guide path.
- Verdict: activation observed; task delivery failed; incident capture and recovery not demonstrated. No evidence the agent received the terminal failure and had an opportunity to report it. Context exhaustion cause remains unverified.

Run: <t3-thread://v2/accda02f-3d0a-44d2-83fd-0d7c4e6e0f3a/thread:delegated-task:command%3Amcp%3A7d56eea7-1a09-4623-ac09-7221fd7a5e74%3Adelegate-task%3A88c0c166-0d8c-4af3-a716-aac044ae8210>

## Gemma 12B

- Requested and recorded model: `litellm/google/gemma-4-12b-qat`, OpenCode, no variant override on the executed retry. An initial launch failed before execution because the catalog-advertised `low` variant was rejected: `Variant unavailable for litellm/google/gemma-4-12b-qat: low`.
- Executed run: 19:34:45–20:04:03 UTC, approximately 29m18s. Harness terminal status completed; agent explicitly reported blocked, so this is not successful task completion.
- All 24 recorded dynamic tool events were `execute` calls; no skill invocation was recorded. No successful local-file read was recorded.
- Early output: `ReferenceError: Unknown identifier 'require'. (line 1, col 12)`.
- Another attempt used an MCP file URI and returned `MCP server not found: opencode`.
- Repeated attempts selected the first search result for `read` without showing that it was the intended local-file tool, then tried incompatible invocation forms.
- Final two tool outputs stopped while reading AGENTS.md: `Unknown tool 'read'. Did you mean tools.opencode.read_mcp_resource?`.
- The write calls appeared later in code branches never reached by those final attempts. The final answer's claim that both read and write actually failed is not substantiated by those outputs. Its diagnosis of broken environment mapping is a hypothesis, not a verified cause.
- After completion the fixture contained only supplied inputs: no result.md or session report.
- Verdict: failed to reach the skill or navigation task; persisted incident capture not demonstrated. The final response did acknowledge blocking errors and lack of artifacts. Repeated similar attempts exceeded the brief's approximately five-minute first-artifact checkpoint, which was advisory rather than enforced.

Run: <t3-thread://v2/accda02f-3d0a-44d2-83fd-0d7c4e6e0f3a/thread:delegated-task:command%3Amcp%3A7d56eea7-1a09-4623-ac09-7221fd7a5e74%3Adelegate-task%3Aecc5a2a2-080f-49aa-971b-d81f59c3f73f>

## Implications and next evaluation

These runs do not establish whether the skill handles ordinary tool errors after loading, and do not establish a model ranking. Neither reached the injected obstacle. One run per model, different effective tool-use paths, unknown capture completeness, and no baseline limit the conclusions.

Recommended next changes for maintainer consideration:

1. Parent/harness capture of pre-start errors and terminal failures. Agent-authored instructions cannot capture events after the agent stops.
2. An early in-progress session record to leave evidence before task/context failure. This alone cannot record the terminal cause.
3. A tool-discovery smoke test for the OpenCode harness, with actual tool schemas and one verified local read/write before evaluating reporting behavior. Avoid inferring that tool-search result order identifies the required capability.
4. A separate bounded evaluation with the skill already loaded and a controlled recoverable error. Keep its results distinct from end-to-end navigation and activation tests. Enforce runtime bounds in orchestration rather than relying only on prompt timing instructions.

No automatic instruction promotion, extra delegates, or model/provider substitutions were performed after these results.
