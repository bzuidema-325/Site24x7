<#
.SYNOPSIS
    Builds the Site24x7 module into ./out/Site24x7 and refreshes the module manifest.

.DESCRIPTION
    Requires the .NET SDK (6.0 or later). The compiled module targets netstandard2.0, so the same
    output loads in Windows PowerShell 5.1 (.NET Framework 4.7.2 or later) and PowerShell 7+.

    The cmdlet and alias lists in module/Site24x7.psd1 are regenerated from the compiled assembly,
    so they always match the code.

.EXAMPLE
    ./build.ps1
    Import-Module ./out/Site24x7/Site24x7.psd1

.EXAMPLE
    ./build.ps1 -Test
    Builds, then runs the Pester tests in ./tests.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $Test
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src/Site24x7/Site24x7.csproj'
$out = Join-Path $root 'out/Site24x7'

dotnet publish $project -c $Configuration -o (Join-Path $root 'out/publish') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
Copy-Item (Join-Path $root 'out/publish/Site24x7.dll') $out
Copy-Item (Join-Path $root 'out/publish/Site24x7.xml') $out -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'module/Site24x7.format.ps1xml') $out

# Read the cmdlets and aliases from the compiled assembly in a separate process,
# so this session never locks the DLL.
$exe = (Get-Process -Id $PID).Path
if ([System.IO.Path]::GetFileNameWithoutExtension($exe) -notin 'pwsh', 'powershell') {
    $exe = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }
}
$listing = & $exe -NoProfile -NonInteractive -Command {
    param($dll)
    $m = Import-Module $dll -PassThru
    [pscustomobject]@{
        Cmdlets = @($m.ExportedCmdlets.Keys | Sort-Object)
        Aliases = @($m.ExportedAliases.Keys | Sort-Object)
    } | ConvertTo-Json -Compress
} -args (Join-Path $out 'Site24x7.dll') | ConvertFrom-Json

$manifestPath = Join-Path $root 'module/Site24x7.psd1'
$manifest = Get-Content $manifestPath -Raw
function Format-PsdList([string[]] $items) {
    "@(`n" + (($items | ForEach-Object { "        '$_'" }) -join "`n") + "`n    )"
}
$manifest = $manifest -replace '(?s)CmdletsToExport\s*=\s*@\(.*?\)', ("CmdletsToExport = " + (Format-PsdList $listing.Cmdlets))
$manifest = $manifest -replace '(?s)AliasesToExport\s*=\s*@\(.*?\)', ("AliasesToExport = " + (Format-PsdList $listing.Aliases))
[System.IO.File]::WriteAllText($manifestPath, $manifest, [System.Text.UTF8Encoding]::new($false))
Copy-Item $manifestPath $out

Write-Host "Built $out ($($listing.Cmdlets.Count) cmdlets, $($listing.Aliases.Count) aliases)"

if ($Test) {
    Import-Module Pester -MinimumVersion 5.0 -MaximumVersion 5.99.99 -ErrorAction Stop
    $result = Invoke-Pester -Path (Join-Path $root 'tests') -Output Detailed -PassThru
    if ($result.FailedCount -gt 0) { throw "$($result.FailedCount) test(s) failed." }
}
