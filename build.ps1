<#
.SYNOPSIS
    Builds the Site24x7 module into ./out/Site24x7.
.DESCRIPTION
    Requires the .NET SDK (8.0 or later; see global.json). The compiled module targets netstandard2.0, so the same
    output loads in Windows PowerShell 5.1 (.NET Framework 4.7.2 or later) and PowerShell 7+.

    The build also:
    - writes Get-Help content (Site24x7.dll-Help.xml) from the cmdlets' doc comments;
    - refreshes the cmdlet list and version in module/Site24x7.psd1 from the compiled assembly and
      the project file, so the manifest always matches the code (CI fails if the checked-in
      manifest is out of date).
.EXAMPLE
    ./build.ps1
    Import-Module ./out/Site24x7/Site24x7.psd1
.EXAMPLE
    ./build.ps1 -Test
    Builds, then runs the Pester tests in ./tests. Needs Pester 5.7.1, the version CI uses:
    Install-Module Pester -RequiredVersion 5.7.1 -Scope CurrentUser
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
$publish = Join-Path $root 'out/publish'
$out = Join-Path $root 'out/Site24x7'

dotnet publish $project -c $Configuration -o $publish --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
Copy-Item (Join-Path $publish 'Site24x7.dll') $out
Copy-Item (Join-Path $root 'module/Site24x7.format.ps1xml') $out

# Anything that loads the DLL runs in a separate process, so this session never locks it.
$exe = (Get-Process -Id $PID).Path
if ([System.IO.Path]::GetFileNameWithoutExtension($exe) -notin 'pwsh', 'powershell') {
    $exe = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }
}

& $exe -NoProfile -NonInteractive -File (Join-Path $root 'tools/Build-Help.ps1') `
    -ModulePath (Join-Path $out 'Site24x7.dll') -DocPath (Join-Path $publish 'Site24x7.xml') -OutFile (Join-Path $out 'Site24x7.dll-Help.xml')
if ($LASTEXITCODE -ne 0) { throw "Building help failed with exit code $LASTEXITCODE" }

$listDll = Join-Path $out 'Site24x7.dll'
$listCommand = "`$m = Import-Module '$($listDll.Replace("'", "''"))' -PassThru; " +
    "[pscustomobject]@{ Cmdlets = @(`$m.ExportedCmdlets.Keys | Sort-Object); Aliases = @(`$m.ExportedAliases.Keys | Sort-Object) } | ConvertTo-Json -Compress"
$listing = & $exe -NoProfile -NonInteractive -Command $listCommand | ConvertFrom-Json
# Never write a manifest that exports nothing because the listing failed.
if ($LASTEXITCODE -ne 0 -or -not $listing -or @($listing.Cmdlets).Count -eq 0) {
    throw "Could not list the module's cmdlets from $listDll; module/Site24x7.psd1 was not changed."
}

# The version lives only in the project file.
[xml] $csproj = Get-Content $project -Raw
$version = ([string]$csproj.Project.PropertyGroup.Version).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Could not read <Version> from $project" }

function Format-PsdList([string[]] $items) {
    if (-not $items) { return '@()' }
    "@(`n" + (($items | ForEach-Object { "        '$_'" }) -join "`n") + "`n    )"
}

$manifestPath = Join-Path $root 'module/Site24x7.psd1'
$manifest = Get-Content $manifestPath -Raw
$manifest = $manifest -replace "ModuleVersion\s*=\s*'[^']*'", "ModuleVersion        = '$version'"
$manifest = $manifest -replace '(?s)CmdletsToExport\s*=\s*@\(.*?\)', ("CmdletsToExport = " + (Format-PsdList $listing.Cmdlets))
$manifest = $manifest -replace '(?s)AliasesToExport\s*=\s*@\(.*?\)', ("AliasesToExport = " + (Format-PsdList $listing.Aliases))
[System.IO.File]::WriteAllText($manifestPath, $manifest, [System.Text.UTF8Encoding]::new($false))
Copy-Item $manifestPath $out

Write-Host "Built $out (version $version, $($listing.Cmdlets.Count) cmdlets)"

if ($Test) {
    Import-Module Pester -RequiredVersion 5.7.1 -ErrorAction Stop
    $result = Invoke-Pester -Path (Join-Path $root 'tests') -Output Detailed -PassThru
    if ($result.FailedCount -gt 0) { throw "$($result.FailedCount) test(s) failed." }
}
