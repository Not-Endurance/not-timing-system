#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  azure_usage_report.sh --resource-id <azure-resource-id> --date <YYYY-MM-DD> [options]

Options:
  --resource-id <id>       Azure resource ID to query.
  --date <YYYY-MM-DD>      Local calendar date to report.
  --timezone <tz>          IANA timezone for the date. Defaults to UTC.
  --metric <name>          Azure Monitor metric name. Defaults to CpuTime for Microsoft.Web/sites.
  --aggregation <type>     Aggregation to query. Defaults to Total.
  --interval <grain>       Time grain. Defaults to PT1H.
  --config-dir <path>      Optional AZURE_CONFIG_DIR path for az CLI scratch/auth files.
  --include-points         Print the raw interval points after the summary.
  -h, --help               Show this help.
EOF
}

resource_id=""
report_date=""
timezone="UTC"
metric=""
aggregation="Total"
interval="PT1H"
config_dir=""
include_points=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --resource-id)
      resource_id="${2:-}"
      shift 2
      ;;
    --date)
      report_date="${2:-}"
      shift 2
      ;;
    --timezone)
      timezone="${2:-}"
      shift 2
      ;;
    --metric)
      metric="${2:-}"
      shift 2
      ;;
    --aggregation)
      aggregation="${2:-}"
      shift 2
      ;;
    --interval)
      interval="${2:-}"
      shift 2
      ;;
    --config-dir)
      config_dir="${2:-}"
      shift 2
      ;;
    --include-points)
      include_points=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$resource_id" || -z "$report_date" ]]; then
  echo "--resource-id and --date are required." >&2
  usage >&2
  exit 2
fi

if [[ ! "$report_date" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]; then
  echo "--date must be in YYYY-MM-DD format." >&2
  exit 2
fi

if [[ -n "$config_dir" ]]; then
  mkdir -p "$config_dir"
  export AZURE_CONFIG_DIR="$config_dir"
fi

if ! command -v az >/dev/null 2>&1; then
  echo "Azure CLI (az) was not found on PATH." >&2
  exit 127
fi

az account show --only-show-errors >/dev/null

next_date="$(TZ="$timezone" date -d "$report_date 1 day" +%F)"
start_epoch="$(TZ="$timezone" date -d "$report_date 00:00:00" +%s)"
end_epoch="$(TZ="$timezone" date -d "$next_date 00:00:00" +%s)"
start_utc="$(date -u -d "@$start_epoch" +%Y-%m-%dT%H:%M:%SZ)"
end_utc="$(date -u -d "@$end_epoch" +%Y-%m-%dT%H:%M:%SZ)"

resource_lower="$(printf '%s' "$resource_id" | tr '[:upper:]' '[:lower:]')"
is_web_app=0
if [[ "$resource_lower" == *"/providers/microsoft.web/sites/"* ]]; then
  is_web_app=1
fi

if [[ -z "$metric" ]]; then
  if [[ "$is_web_app" -eq 1 ]]; then
    metric="CpuTime"
  else
    echo "--metric is required unless the resource is Microsoft.Web/sites." >&2
    exit 2
  fi
fi

metric_display="$(az monitor metrics list-definitions \
  --resource "$resource_id" \
  --query "[?name.value=='$metric'] | [0].name.localizedValue" \
  -o tsv \
  --only-show-errors 2>/dev/null | tr -d '\r' || true)"
metric_unit="$(az monitor metrics list-definitions \
  --resource "$resource_id" \
  --query "[?name.value=='$metric'] | [0].unit" \
  -o tsv \
  --only-show-errors 2>/dev/null | tr -d '\r' || true)"
metric_primary="$(az monitor metrics list-definitions \
  --resource "$resource_id" \
  --query "[?name.value=='$metric'] | [0].primaryAggregationType" \
  -o tsv \
  --only-show-errors 2>/dev/null | tr -d '\r' || true)"

points="$(az monitor metrics list \
  --resource "$resource_id" \
  --metric "$metric" \
  --aggregation "$aggregation" \
  --interval "$interval" \
  --start-time "$start_utc" \
  --end-time "$end_utc" \
  --query "value[0].timeseries[0].data[].[timeStamp,total,average,maximum,minimum]" \
  -o tsv \
  --only-show-errors | tr -d '\r')"

stats="$(printf '%s\n' "$points" | awk -v agg="$aggregation" '
BEGIN {
  FS = "\t";
  sum = 0;
  count = 0;
  nonzero = 0;
  max = 0;
  max_time = "";
}
NF > 0 && $1 != "" {
  value = "";
  if (agg == "Total") value = $2;
  else if (agg == "Average") value = $3;
  else if (agg == "Maximum") value = $4;
  else if (agg == "Minimum") value = $5;
  else value = $2;

  numeric = value == "" ? 0 : value + 0;
  sum += numeric;
  count += 1;
  if (value != "" && numeric != 0) nonzero += 1;
  if (max_time == "" || numeric > max) {
    max = numeric;
    max_time = $1;
  }
}
END {
  printf "%.6f\t%d\t%d\t%.6f\t%s\n", sum, count, nonzero, max, max_time;
}')"

IFS=$'\t' read -r total point_count nonzero_count max_value max_time <<<"$stats"

server_farm_id=""
plan_summary=""
if [[ "$is_web_app" -eq 1 ]]; then
  server_farm_id="$(az resource show \
    --ids "$resource_id" \
    --api-version 2023-12-01 \
    --query "properties.serverFarmId" \
    -o tsv \
    --only-show-errors 2>/dev/null | tr -d '\r' || true)"

  if [[ -n "$server_farm_id" && "$server_farm_id" != "null" ]]; then
    plan_name="$(az appservice plan show \
      --ids "$server_farm_id" \
      --query "name" \
      -o tsv \
      --only-show-errors 2>/dev/null | tr -d '\r' || true)"
    plan_sku="$(az appservice plan show \
      --ids "$server_farm_id" \
      --query "sku.name" \
      -o tsv \
      --only-show-errors 2>/dev/null | tr -d '\r' || true)"
    plan_tier="$(az appservice plan show \
      --ids "$server_farm_id" \
      --query "sku.tier" \
      -o tsv \
      --only-show-errors 2>/dev/null | tr -d '\r' || true)"
    plan_capacity="$(az appservice plan show \
      --ids "$server_farm_id" \
      --query "sku.capacity" \
      -o tsv \
      --only-show-errors 2>/dev/null | tr -d '\r' || true)"
    plan_summary="${plan_name}"$'\t'"${plan_sku}"$'\t'"${plan_tier}"$'\t'"${plan_capacity}"
  fi
fi

echo "# Azure Usage Report"
echo
echo "- Resource: \`$resource_id\`"
echo "- Local date: \`$report_date\` in \`$timezone\`"
echo "- UTC window: \`$start_utc\` to \`$end_utc\`"
echo "- Metric: \`${metric}\`${metric_display:+ ($metric_display)}"
echo "- Aggregation: \`$aggregation\`"
if [[ -n "$metric_primary" ]]; then
  echo "- Primary aggregation: \`$metric_primary\`"
fi
echo "- Interval: \`$interval\`"
echo "- Unit: \`${metric_unit:-unknown}\`"
echo "- Data points: \`$point_count\` total, \`$nonzero_count\` non-zero"
if [[ "$aggregation" == "Total" ]]; then
  echo "- Total: \`$total\`${metric_unit:+ $metric_unit}"
else
  echo "- Sum of interval values: \`$total\`${metric_unit:+ $metric_unit}"
fi

if [[ "$metric" == "CpuTime" && "$metric_unit" == "Seconds" ]]; then
  cpu_minutes="$(awk -v s="$total" 'BEGIN { printf "%.3f", s / 60 }')"
  cpu_hours="$(awk -v s="$total" 'BEGIN { printf "%.4f", s / 3600 }')"
  echo "- CPU conversion: \`$cpu_minutes\` CPU minutes / \`$cpu_hours\` CPU hours"
fi

if [[ -n "$max_time" ]]; then
  echo "- Peak interval: \`$max_value\` at \`$max_time\`"
fi

if [[ -n "$server_farm_id" && "$server_farm_id" != "null" ]]; then
  echo "- App Service plan: \`$server_farm_id\`"
fi

if [[ -n "$plan_summary" ]]; then
  IFS=$'\t' read -r plan_name plan_sku plan_tier plan_capacity <<<"$plan_summary"
  echo "- Plan SKU: \`${plan_name:-unknown}\` / \`${plan_sku:-unknown}\` / \`${plan_tier:-unknown}\` / capacity \`${plan_capacity:-unknown}\`"
fi

if [[ "$include_points" -eq 1 ]]; then
  echo
  echo "## Points"
  echo
  echo '```tsv'
  printf '%s\n' "$points"
  echo '```'
fi
