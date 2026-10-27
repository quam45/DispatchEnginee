# Creates or upgrades the DispatchEngine database from the scripts in this folder.
# Every script is idempotent, so this is safe to re-run on an existing database.
#   .\Database\deploy.ps1
#   .\Database\deploy.ps1 -Server "localhost\SQLEXPRESS" -Database DispatchEngine
param(
    [string]$Server = "localhost\SQLEXPRESS",
    [string]$Database = "DispatchEngine"
)
$ErrorActionPreference = "Stop"

sqlcmd -S $Server -E -b -Q "IF DB_ID('$Database') IS NULL CREATE DATABASE [$Database]"
if ($LASTEXITCODE -ne 0) { throw "Could not connect to $Server" }

$files = @(Get-ChildItem "$PSScriptRoot\Tables\*.sql" | Sort-Object Name) +
         @(Get-ChildItem "$PSScriptRoot\Scripts\*.sql" | Sort-Object Name) +
         @(Get-ChildItem "$PSScriptRoot\StoredProcedures\*.sql" | Sort-Object Name)

foreach ($file in $files) {
    Write-Host "-> $($file.Name)"
    # -I turns on QUOTED_IDENTIFIER, which filtered indexes (UX_Users_Email, UX_Orders_ActiveRider)
    # require for any procedure that writes to those tables.
    sqlcmd -S $Server -E -d $Database -b -I -i $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Failed: $($file.Name)" }
}
Write-Host "Database '$Database' is up to date."
