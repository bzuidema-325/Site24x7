<#
.SYNOPSIS
    Checks the API paths marked "// verify" in src/Site24x7/Core/Endpoints.cs against a live account.

.DESCRIPTION
    Some API paths could not be checked against the Site24x7 documentation when the module was
    written. This script sends one read-only GET to each of them with your current session and
    reports which ones Site24x7 recognises. It never creates, changes or deletes anything; paths
    that can only be called with POST or PUT (such as starting maintenance by name) are listed as
    not checked.

    Connect first with Connect-Site24x7. When a path is confirmed, remove its "// verify" comment
    in Endpoints.cs and run tools/Build-ResourceCmdlets.ps1, so the help stops calling it unverified.

.EXAMPLE
    Import-Module ./out/Site24x7/Site24x7.psd1
    Connect-Site24x7 -ClientId $id -ClientSecret $secret -RefreshToken $refresh
    ./tools/Test-Site24x7Endpoints.ps1 | Format-Table
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if (-not (Get-Command Get-Site24x7Session -ErrorAction SilentlyContinue) -or -not (Get-Site24x7Session)) {
    throw 'Import the module and run Connect-Site24x7 first.'
}

# Which constants the generated cmdlets list with GET; anything else is only called with POST or PUT.
$table = & (Join-Path $PSScriptRoot 'Build-ResourceCmdlets.ps1') -TableOnly
$readable = @{}
foreach ($r in $table) { if ($r.Verbs -contains 'Get') { $readable[$r.Path] = "Get-Site24x7$($r.Noun)" } }

$endpointsFile = Join-Path $PSScriptRoot '../src/Site24x7/Core/Endpoints.cs'
foreach ($line in Get-Content $endpointsFile) {
    if ($line -notmatch 'public const string (\w+) = "([^"]*)";(.*)$') { continue }
    $name, $path, $comment = $Matches[1], $Matches[2], $Matches[3]
    if ($comment -notmatch '\bverify\b') { continue }

    if (-not $readable.ContainsKey($name)) {
        [pscustomobject]@{ Constant = $name; Path = $path; Cmdlet = $null; Result = 'Not checked'; Detail = 'Only called with POST or PUT, which would change something.' }
        continue
    }

    try {
        $data = Invoke-Site24x7Api GET $path -ErrorAction Stop
        $count = @($data | Where-Object { $_ -ne $null }).Count
        [pscustomobject]@{ Constant = $name; Path = $path; Cmdlet = $readable[$name]; Result = 'OK'; Detail = "$count item(s)" }
    } catch {
        $ex = $_.Exception
        $status = if ($ex.PSObject.Properties['StatusCode'] -and $ex.StatusCode) { [int]$ex.StatusCode } else { $null }
        # 404 means the path is wrong. 401 and 403 usually mean the path exists but your plan or
        # permissions do not cover it, so the path itself cannot be judged.
        $result = switch ($status) {
            404 { 'Wrong path' }
            { $_ -in 401, 403 } { 'No access' }
            default { 'Error' }
        }
        [pscustomobject]@{ Constant = $name; Path = $path; Cmdlet = $readable[$name]; Result = $result; Detail = $ex.Message }
    }
}
