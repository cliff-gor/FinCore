#!/usr/bin/env bash
# Runs FinCore.Migrator (master spec §33.10): applies every module's EF Core migrations
# in the fixed module order. Locally the API also migrates at startup in Development;
# this script is for running the migrator on its own (and mirrors the one-off ECS task on AWS).
#
# Usage:
#   deploy/scripts/run-migrations.sh                 # run from source (dotnet run)
#   MODE=dll  deploy/scripts/run-migrations.sh       # run a published dll (MIGRATOR_DLL)
#   MODE=image IMAGE=fincore-migrator:local deploy/scripts/run-migrations.sh   # run the container
#
# Env:
#   CONNECTION_STRING  Npgsql connection string. Default: the local docker-compose Postgres
#                      (DEV-ONLY credentials from deploy/docker-compose.yml).
#   MIGRATOR_PROJECT   Default: src/Tools/FinCore.Migrator
#   MIGRATOR_DLL       Default: artifacts/migrator/FinCore.Migrator.dll
#   IMAGE              Container image for MODE=image. Default: fincore-migrator:local
#   DOCKER_NETWORK     Optional Docker network for MODE=image. Host networking is not portable,
#                      so the default connection string uses host.docker.internal in that mode.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MODE="${MODE:-project}"
MIGRATOR_PROJECT="${MIGRATOR_PROJECT:-src/Tools/FinCore.Migrator}"
MIGRATOR_DLL="${MIGRATOR_DLL:-artifacts/migrator/FinCore.Migrator.dll}"
IMAGE="${IMAGE:-fincore-migrator:local}"

# DEV-ONLY default (matches deploy/docker-compose.yml). Never used outside a local machine.
default_host="localhost"
if [[ "${MODE}" == "image" ]]; then default_host="host.docker.internal"; fi
CONNECTION_STRING="${CONNECTION_STRING:-Host=${default_host};Port=5432;Database=fincore;Username=fincore;Password=fincore_dev_only}"

# .NET maps ConnectionStrings__fincore to configuration key ConnectionStrings:fincore.
export ConnectionStrings__fincore="${CONNECTION_STRING}"

cd "${REPO_ROOT}"
case "${MODE}" in
  project)
    echo "Running migrator from source (${MIGRATOR_PROJECT})"
    dotnet run --project "${MIGRATOR_PROJECT}" -c Release
    ;;
  dll)
    echo "Running migrator dll (${MIGRATOR_DLL})"
    dotnet "${MIGRATOR_DLL}"
    ;;
  image)
    echo "Running migrator container (${IMAGE})"
    network_args=()
    if [[ -n "${DOCKER_NETWORK:-}" ]]; then network_args=(--network "${DOCKER_NETWORK}"); fi
    docker run --rm ${network_args[@]+"${network_args[@]}"} \
      --add-host=host.docker.internal:host-gateway \
      -e ConnectionStrings__fincore \
      "${IMAGE}"
    ;;
  *)
    echo "Unknown MODE '${MODE}' (expected project, dll or image)" >&2
    exit 2
    ;;
esac
echo "Migrations applied."
