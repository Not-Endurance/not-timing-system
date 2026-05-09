---
name: azure-resource-usage
description: Generate Azure resource usage reports from Azure Monitor metrics when the user provides an Azure resource ID, date or date range, timezone, and usage intent such as Web App CPU time, request volume, quota investigation, cost-adjacent usage, or metric summaries. Use for Azure App Service/Web App resource usage questions, historical CPU or request reports, resource metric discovery, and repeatable Azure CLI report generation.
---

# Azure Resource Usage

## Overview

Use Azure Monitor metrics to produce a concise, auditable usage report for a specific Azure resource and calendar date. Prefer read-only Azure CLI commands; do not change Azure resources.

## Required Inputs

Ask for missing values only when they cannot be inferred safely:

- Azure resource ID, for example `/subscriptions/.../resourceGroups/.../providers/Microsoft.Web/sites/...`
- Date in `YYYY-MM-DD`, or an explicit UTC start/end range
- Timezone for date interpretation, for example `Europe/Sofia` or `UTC`
- Usage intent, for example `Web App CPU`, `requests`, `memory`, `quota`, or a specific Azure Monitor metric name

For this repo, keep CLI scratch files inside the workspace by passing `--config-dir "$PWD/.az-codex"` to the bundled script or exporting `AZURE_CONFIG_DIR="$PWD/.az-codex"` for direct `az` commands. Remove that scratch directory after the report when it was created only for the query.

## Workflow

1. Confirm Azure CLI access with `az account show --only-show-errors`.
2. Resolve the local calendar day to a UTC window. Azure Monitor metric timestamps are UTC; never assume the user's date is a UTC date unless they say so.
3. Discover relevant metrics with `az monitor metrics list-definitions --resource "$RID"` when the metric is not explicit.
4. Query with `az monitor metrics list --resource "$RID" --metric <name> --aggregation <aggregation> --interval <grain> --start-time <utc> --end-time <utc>`.
5. Sum `total` values for usage metrics such as `CpuTime`; use average/min/max only when the metric definition or user request calls for it.
6. Report the resource, UTC window, metric name, unit, aggregation, total, peak interval, and any known plan/SKU context.
7. Cite Microsoft Learn when interpreting quotas, billing, or metric semantics; use official Microsoft sources only for current Azure behavior.

## Web App CPU Profile

For `Microsoft.Web/sites` Web Apps:

- Default metric: `CpuTime`
- Default aggregation: `Total`
- Unit: `Seconds`
- Useful interval: `PT1H` for a daily report, `PT5M` for peak detail
- Plan lookup: get `properties.serverFarmId` from `az resource show`, then run `az appservice plan show --ids "$PLAN_ID"`

Do not use Azure Functions billing metrics such as `FunctionExecutionUnits` for a plain Web App unless the resource is actually a Functions workload and the user asks for that view.

## Bundled Script

Use `scripts/azure_usage_report.sh` for the common report:

```bash
.codex/skills/azure-resource-usage/scripts/azure_usage_report.sh \
  --resource-id "$RID" \
  --date 2026-04-10 \
  --timezone Europe/Sofia \
  --metric CpuTime \
  --aggregation Total \
  --interval PT1H \
  --config-dir "$PWD/.az-codex"
```

If `--metric` is omitted for `Microsoft.Web/sites`, the script defaults to `CpuTime`. For other resource types, provide the metric explicitly or discover definitions first.

## Report Shape

Keep the final answer short and explicit:

- State the local date and UTC window used.
- State the metric name, aggregation, and unit.
- Give the total in the native unit and a human-friendly conversion when useful.
- Include peak interval and plan/SKU context when available.
- Mention failed or missing data clearly, including permission or retention limits.
