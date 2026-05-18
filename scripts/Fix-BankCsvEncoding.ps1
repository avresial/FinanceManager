<#
.SYNOPSIS
    Re-encodes a bank-export CSV from Windows-1250 to UTF-8 so Polish
    diacritics (ą ć ę ł ń ó ś ź ż and capitals) display correctly.

.DESCRIPTION
    Bank exports come down as CP1250 but tools open them as CP1252/UTF-8,
    mangling every Polish character. This script reads the file as
    Windows-1250 and rewrites it as UTF-8 (with BOM, so Excel honours it).
    The original is preserved alongside as <name>.bak.

.PARAMETER Path
    Path to the CSV to fix. Rewritten in place.

.PARAMETER NoBackup
    Skip writing a .bak copy.

.EXAMPLE
    .\Fix-BankCsvEncoding.ps1 -Path "$env:USERPROFILE\Downloads\Historia_Rachunku_260517_113557.csv"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Path,

    [switch]$NoBackup
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "File not found: $Path"
}

$resolved = (Resolve-Path -LiteralPath $Path).ProviderPath
$bytes    = [System.IO.File]::ReadAllBytes($resolved)
$cp1250   = [System.Text.Encoding]::GetEncoding(1250)
$text     = $cp1250.GetString($bytes)

if (-not $NoBackup) {
    $backup = "$resolved.bak"
    [System.IO.File]::WriteAllBytes($backup, $bytes)
    Write-Host "Backup written: $backup"
}

$utf8Bom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($resolved, $text, $utf8Bom)

Write-Host "Converted to UTF-8: $resolved"
