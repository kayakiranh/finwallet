#!/usr/bin/env bash
set -euo pipefail

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
if [[ ! -x "${SQLCMD}" ]]; then
  SQLCMD="/opt/mssql-tools/bin/sqlcmd"
fi

if [[ ! -x "${SQLCMD}" ]]; then
  echo "sqlcmd was not found in the SQL Server container image." >&2
  exit 1
fi

: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is required}"

SQLCMD_COMMON=(
  -S "mssql,1433"
  -U "sa"
  -P "${MSSQL_SA_PASSWORD}"
  -C
  -b
  -l 30
  -I
)

run_query() {
  "${SQLCMD}" "${SQLCMD_COMMON[@]}" "$@"
}

echo "Ensuring FinWallet database exists..."
run_query -d master -Q "IF DB_ID(N'FinWallet') IS NULL CREATE DATABASE [FinWallet];"

run_query -d FinWallet -Q "
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions
    (
        Version VARCHAR(32) NOT NULL CONSTRAINT PK_SchemaVersions PRIMARY KEY,
        AppliedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAt DEFAULT SYSUTCDATETIME()
    );
END;"

apply_migration() {
  local version="$1"
  local file="$2"
  local already_applied

  already_applied=$(run_query -d FinWallet -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(1) FROM dbo.SchemaVersions WHERE Version='${version}';" | tr -d '\r[:space:]')

  if [[ "${already_applied}" == "1" ]]; then
    echo "Migration ${version} already applied."
    return
  fi

  echo "Applying migration ${version}: ${file}"
  # -I in SQLCMD_COMMON enables QUOTED_IDENTIFIER for filtered/computed index creation.
  run_query -d FinWallet -i "/database/${file}"
  run_query -d FinWallet -Q "INSERT INTO dbo.SchemaVersions (Version) VALUES ('${version}');"
}

apply_migration "001" "001_authentication_schema.sql"
apply_migration "002" "002_financial_accounts_schema.sql"
apply_migration "003" "003_ledger_transaction_schema.sql"
apply_migration "004" "004_project_completion_schema.sql"

echo "FinWallet database schema is ready."
