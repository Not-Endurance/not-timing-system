# Authenticates the CLI agains ar-infrastructure-monitor registration with Readonly access to infrastructure
read -r -s -p "Client secret: " AZ_SECRET; echo
export AZURE_CONFIG_DIR="$(git rev-parse --show-toplevel)/.tmp/infrastructure-monitor"
az login --service-principal -u 7d22f4e0-5c8a-4cd4-bc91-cc1d88abf76f -p "$AZ_SECRET" --tenant ac119374-ae0a-452e-b13c-2ac6d3b72d8e --only-show-errors
unset AZ_SECRET