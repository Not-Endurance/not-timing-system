#!/usr/bin/env bash
set -euo pipefail

NODE_VERSION="${NODE_VERSION:-22.16.0}"
CORE_TOOLS_VERSION="${CORE_TOOLS_VERSION:-4.10.0}"
CORE_TOOLS_BUILD="${CORE_TOOLS_BUILD:-4.0.273114}"
DOCKER_CLI="${DOCKER_CLI:-/mnt/wsl/docker-desktop/cli-tools/usr/bin/docker}"

skip_install=false
skip_shim=false
skip_bashrc=false
verify_only=false
run_tests=false

usage() {
  cat <<'USAGE'
Usage: setup_wsl_integration.sh [options]

Options:
  --verify-only   Check current WSL integration tool setup without installing or editing files.
  --skip-install  Do not install repo-local Node or Azure Functions Core Tools.
  --skip-shim     Do not create ~/.local/bin/func.
  --skip-bashrc   Do not edit ~/.bashrc.
  --run-tests     Run the integration build and tests after setup.
  -h, --help      Show this help.

Environment overrides:
  NODE_VERSION         Default: 22.16.0
  CORE_TOOLS_VERSION   Default: 4.10.0
  CORE_TOOLS_BUILD     Default: 4.0.273114
  DOCKER_CLI           Default: /mnt/wsl/docker-desktop/cli-tools/usr/bin/docker
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --verify-only)
      verify_only=true
      ;;
    --skip-install)
      skip_install=true
      ;;
    --skip-shim)
      skip_shim=true
      ;;
    --skip-bashrc)
      skip_bashrc=true
      ;;
    --run-tests)
      run_tests=true
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
  shift
done

repo_root() {
  local current="$PWD"
  while [[ "$current" != "/" ]]; do
    if [[ -f "$current/AGENTS.md" && -d "$current/tests/NTS.Tests.Integration" ]]; then
      printf '%s\n' "$current"
      return
    fi
    current="$(dirname "$current")"
  done
  echo "Could not locate repository root. Run from not-timing-system." >&2
  exit 1
}

ROOT="$(repo_root)"
TOOLS="$ROOT/.tools"
NODE_DIR="$TOOLS/node"
NPM_PREFIX="$TOOLS/npm"
NPM_CACHE="$TOOLS/npm-cache"
DOWNLOADS="$TOOLS/downloads"
FUNC_BIN="$NPM_PREFIX/node_modules/azure-functions-core-tools/bin/func"
LOCAL_BIN="$HOME/.local/bin"
FUNC_SHIM="$LOCAL_BIN/func"
PATH_LINE='export PATH="/mnt/wsl/docker-desktop/cli-tools/usr/bin:$HOME/.local/bin:$PATH"'

log() {
  printf '[nts-wsl-integration] %s\n' "$*"
}

verify() {
  log "Repository: $ROOT"

  if [[ -x "$DOCKER_CLI" ]]; then
    "$DOCKER_CLI" version --format 'Docker Client {{.Client.Version}} -> Server {{.Server.Version}}'
  else
    echo "Missing Docker Desktop WSL CLI: $DOCKER_CLI" >&2
    return 1
  fi

  if [[ -x "$FUNC_BIN" ]]; then
    "$FUNC_BIN" --version
  elif command -v func >/dev/null 2>&1; then
    func --version
  else
    echo "func is not available. Run setup without --verify-only." >&2
    return 1
  fi

  dotnet --version
}

install_tools() {
  mkdir -p "$DOWNLOADS" "$NODE_DIR" "$NPM_PREFIX" "$NPM_CACHE"

  if [[ ! -x "$NODE_DIR/bin/node" ]]; then
    local node_archive="node-v${NODE_VERSION}-linux-x64.tar.xz"
    log "Downloading Node $NODE_VERSION"
    curl -fL "https://nodejs.org/dist/v${NODE_VERSION}/${node_archive}" -o "$DOWNLOADS/$node_archive"
    tar -xJf "$DOWNLOADS/$node_archive" -C "$NODE_DIR" --strip-components=1
  fi

  if [[ ! -d "$NPM_PREFIX/node_modules/azure-functions-core-tools" ]]; then
    log "Installing azure-functions-core-tools npm package metadata"
    PATH="$NODE_DIR/bin:$PATH" npm install \
      --prefix "$NPM_PREFIX" \
      --cache "$NPM_CACHE" \
      --no-save \
      --ignore-scripts \
      azure-functions-core-tools@4
  fi

  if [[ ! -x "$FUNC_BIN" ]]; then
    local zip_name="Azure.Functions.Cli.linux-x64.${CORE_TOOLS_VERSION}.zip"
    log "Downloading Azure Functions Core Tools $CORE_TOOLS_VERSION"
    curl -fL "https://cdn.functions.azure.com/public/${CORE_TOOLS_BUILD}/${zip_name}" -o "$DOWNLOADS/$zip_name"

    rm -rf "$NPM_PREFIX/node_modules/azure-functions-core-tools/bin"
    mkdir -p "$NPM_PREFIX/node_modules/azure-functions-core-tools/bin"

    log "Extracting Core Tools; this can take several minutes on /mnt/* filesystems"
    PATH="$NODE_DIR/bin:$PATH" node -e "const extract=require(process.argv[1]); extract(process.argv[2], {dir: process.argv[3]}).then(()=>console.log('extracted'))" \
      "$NPM_PREFIX/node_modules/azure-functions-core-tools/node_modules/extract-zip" \
      "$DOWNLOADS/$zip_name" \
      "$NPM_PREFIX/node_modules/azure-functions-core-tools/bin"

    chmod +x \
      "$NPM_PREFIX/node_modules/azure-functions-core-tools/bin/func" \
      "$NPM_PREFIX/node_modules/azure-functions-core-tools/bin/gozip" \
      "$NPM_PREFIX/node_modules/azure-functions-core-tools/bin/in-proc8/func" \
      "$NPM_PREFIX/node_modules/azure-functions-core-tools/bin/in-proc6/func"
  fi
}

create_shim() {
  mkdir -p "$LOCAL_BIN"
  cat > "$FUNC_SHIM" <<EOF
#!/usr/bin/env bash
exec "$FUNC_BIN" "\$@"
EOF
  chmod +x "$FUNC_SHIM"
  log "Created $FUNC_SHIM"
}

update_bashrc() {
  if [[ ! -f "$HOME/.bashrc" ]] || ! grep -qxF "$PATH_LINE" "$HOME/.bashrc"; then
    printf '\n# NTS WSL tools\n%s\n' "$PATH_LINE" >> "$HOME/.bashrc"
    log "Updated $HOME/.bashrc"
  fi
}

start_azurite() {
  "$DOCKER_CLI" start azurite >/dev/null 2>&1 || "$DOCKER_CLI" run -d \
    --name azurite \
    -p 10000:10000 \
    -p 10001:10001 \
    -p 10002:10002 \
    mcr.microsoft.com/azure-storage/azurite:3.35.0 \
    azurite \
    --blobHost 0.0.0.0 \
    --queueHost 0.0.0.0 \
    --tableHost 0.0.0.0 >/dev/null
}

if [[ "$verify_only" == true ]]; then
  verify
  exit 0
fi

if [[ "$skip_install" != true ]]; then
  install_tools
fi

if [[ "$skip_shim" != true ]]; then
  create_shim
fi

if [[ "$skip_bashrc" != true ]]; then
  update_bashrc
fi

start_azurite
verify

if [[ "$run_tests" == true ]]; then
  export PATH="/mnt/wsl/docker-desktop/cli-tools/usr/bin:$HOME/.local/bin:$PATH"
  dotnet build "$ROOT/tests/NTS.Tests.Integration/NTS.Tests.Integration.csproj" -v:q
  dotnet test "$ROOT/tests/NTS.Tests.Integration/NTS.Tests.Integration.csproj" -v:m
fi
