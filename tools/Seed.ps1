<#
.SYNOPSIS
  Seeds CabinGuests and PreferenceCatalog DynamoDB tables from JSON in tools/seed/.

.EXAMPLE
  ./tools/Seed.ps1 -GuestsTable cabinbingo-CabinGuestsTable-xxxxx -PreferencesTable cabinbingo-PreferenceCatalogTable-yyyyy
#>
param(
  [string] $Region = "us-east-2",
  [Parameter(Mandatory = $true)][string] $GuestsTable,
  [Parameter(Mandatory = $true)][string] $PreferencesTable
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$seed = Join-Path $root "tools\seed"

Write-Host "Seeding guests -> $GuestsTable"
Get-ChildItem $seed -Filter "guest_*.json" | ForEach-Object {
  $uri = ([System.Uri](Resolve-Path $_.FullName)).AbsoluteUri
  aws dynamodb put-item --region $Region --table-name $GuestsTable --item $uri | Out-Null
  Write-Host "  $($_.Name)"
}

Write-Host "Seeding preferences -> $PreferencesTable"
Get-ChildItem $seed -Filter "pref_*.json" | ForEach-Object {
  $uri = ([System.Uri](Resolve-Path $_.FullName)).AbsoluteUri
  aws dynamodb put-item --region $Region --table-name $PreferencesTable --item $uri | Out-Null
  Write-Host "  $($_.Name)"
}

Write-Host "Done."
