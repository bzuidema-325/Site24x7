<#
.SYNOPSIS
    Builds Site24x7.dll-Help.xml (MAML help for Get-Help) from the compiled module and its XML doc comments.

.DESCRIPTION
    Each cmdlet class carries its help in /// comments:
      <summary><para type="synopsis">...</para><para type="description">...</para></summary>
      <example><code>...</code></example>
    and each parameter property has a <summary>. This script reads the compiler's documentation
    file, matches it to the cmdlets the module exports, and writes the MAML file PowerShell looks
    for next to a binary module. build.ps1 runs it in a separate process so the DLL is not locked.

.EXAMPLE
    ./tools/Build-Help.ps1 -ModulePath ./out/Site24x7/Site24x7.dll -DocPath ./out/publish/Site24x7.xml -OutFile ./out/Site24x7/Site24x7.dll-Help.xml
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ModulePath,
    [Parameter(Mandatory)] [string] $DocPath,
    [Parameter(Mandatory)] [string] $OutFile
)
$ErrorActionPreference = 'Stop'

$module = Import-Module $ModulePath -PassThru -Force
[xml] $doc = Get-Content -Raw -LiteralPath $DocPath
$members = @{}
foreach ($m in $doc.doc.members.member) { $members[$m.name] = $m }

$common = [System.Management.Automation.Internal.CommonParameters].GetProperties().Name +
    [System.Management.Automation.Internal.ShouldProcessParameters].GetProperties().Name

# PowerShell's XML adapter returns a plain string for an element that holds only text.
function Get-Text($node) {
    if (-not $node) { return '' }
    $text = if ($node -is [string]) { $node } else { $node.InnerText }
    (($text -split "`r?`n") | ForEach-Object { $_.Trim() }) -join ' ' -replace '\s{2,}', ' '
}

function Get-TypeDoc([type] $type) { $members["T:$($type.FullName)"] }

# A parameter's summary, from the class that declares it or, for an override without one, from a base class.
function Get-ParameterDoc([type] $type, [string] $name) {
    for ($t = $type; $t; $t = $t.BaseType) {
        $p = $t.GetProperty($name, [System.Reflection.BindingFlags] 'Public, Instance, DeclaredOnly')
        if ($p) {
            $m = $members["P:$($p.DeclaringType.FullName).$name"]
            if ($m -and $m.summary) { return (Get-Text $m.summary) }
        }
    }
    ''
}

function Get-TypeName([type] $type) {
    if ($type -eq [System.Management.Automation.SwitchParameter]) { return 'SwitchParameter' }
    $type.Name
}

$settings = [System.Xml.XmlWriterSettings]@{ Indent = $true; Encoding = [System.Text.UTF8Encoding]::new($false) }
$dir = Split-Path $OutFile -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$w = [System.Xml.XmlWriter]::Create($OutFile, $settings)
$maml = 'http://schemas.microsoft.com/maml/2004/10'
$cmd = 'http://schemas.microsoft.com/maml/dev/command/2004/10'
$dev = 'http://schemas.microsoft.com/maml/dev/2004/10'

function Para([string] $text) {
    $w.WriteStartElement('maml', 'para', $maml); $w.WriteString($text); $w.WriteEndElement()
}

function Description([string] $text) {
    $w.WriteStartElement('maml', 'description', $maml); Para $text; $w.WriteEndElement()
}

function Write-ParameterElement($info, [string] $position, [bool] $required, [bool] $full, [type] $type) {
    $p = $info
    $pipeline = @()
    if ($p.ValueFromPipeline) { $pipeline += 'ByValue' }
    if ($p.ValueFromPipelineByPropertyName) { $pipeline += 'ByPropertyName' }
    $w.WriteStartElement('command', 'parameter', $cmd)
    $w.WriteAttributeString('required', $required.ToString().ToLowerInvariant())
    $globbing = [bool]($p.Attributes | Where-Object { $_ -is [System.Management.Automation.SupportsWildcardsAttribute] })
    $w.WriteAttributeString('globbing', $globbing.ToString().ToLowerInvariant())
    $w.WriteAttributeString('pipelineInput', $(if ($pipeline) { "True ($($pipeline -join ', '))" } else { 'False' }))
    $w.WriteAttributeString('position', $position)
    if ($full -and $p.Aliases.Count) { $w.WriteAttributeString('aliases', ($p.Aliases -join ', ')) }
    $w.WriteElementString('name', $maml, $p.Name)
    if ($full) { Description (Get-ParameterDoc $type $p.Name) }
    if ($p.ParameterType -ne [System.Management.Automation.SwitchParameter]) {
        $w.WriteStartElement('command', 'parameterValue', $cmd)
        $w.WriteAttributeString('required', 'true')
        $w.WriteString((Get-TypeName $p.ParameterType))
        $w.WriteEndElement()
    }
    if ($full) {
        $w.WriteStartElement('dev', 'type', $dev); $w.WriteElementString('name', $maml, $p.ParameterType.FullName); $w.WriteEndElement()
    }
    $w.WriteEndElement()
}

$w.WriteStartDocument()
$w.WriteStartElement('helpItems', 'http://msh')
$w.WriteAttributeString('schema', 'maml')

$missing = [System.Collections.Generic.List[string]]::new()
foreach ($c in ($module.ExportedCmdlets.Values | Sort-Object Name)) {
    $type = $c.ImplementingType
    $typeDoc = Get-TypeDoc $type
    $synopsis = Get-Text ($typeDoc.summary.para | Where-Object type -eq 'synopsis')
    $description = Get-Text ($typeDoc.summary.para | Where-Object type -eq 'description')
    if (-not $synopsis) { $missing.Add("$($c.Name): synopsis") }

    $w.WriteStartElement('command', 'command', $cmd)
    $w.WriteAttributeString('xmlns', 'maml', $null, $maml)
    $w.WriteAttributeString('xmlns', 'command', $null, $cmd)
    $w.WriteAttributeString('xmlns', 'dev', $null, $dev)

    $w.WriteStartElement('command', 'details', $cmd)
    $w.WriteElementString('name', $cmd, $c.Name)
    $w.WriteElementString('verb', $cmd, $c.Verb)
    $w.WriteElementString('noun', $cmd, $c.Noun)
    Description $synopsis
    $w.WriteEndElement()

    Description $(if ($description) { $description } else { $synopsis })

    # Syntax: one item per parameter set.
    $w.WriteStartElement('command', 'syntax', $cmd)
    foreach ($set in $c.ParameterSets) {
        $w.WriteStartElement('command', 'syntaxItem', $cmd)
        $w.WriteElementString('name', $maml, $c.Name)
        foreach ($p in ($set.Parameters | Where-Object Name -notin $common)) {
            $pos = if ($p.Position -ge 0) { [string]$p.Position } else { 'named' }
            Write-ParameterElement $p $pos $p.IsMandatory $false $type
        }
        $w.WriteEndElement()
    }
    $w.WriteEndElement()

    # Parameters: each once, with its description.
    $w.WriteStartElement('command', 'parameters', $cmd)
    foreach ($name in ($c.Parameters.Keys | Where-Object { $_ -notin $common } | Sort-Object)) {
        $meta = $c.Parameters[$name]
        $setInfo = foreach ($set in $c.ParameterSets) { $set.Parameters | Where-Object Name -eq $name }
        $first = @($setInfo)[0]
        $pos = if ($first.Position -ge 0) { [string]$first.Position } else { 'named' }
        $required = [bool](@($setInfo) | Where-Object IsMandatory)
        $text = Get-ParameterDoc $type $name
        if (-not $text -and $name -notin 'Session', 'Property', 'PassThru', 'Name', 'Query') { $missing.Add("$($c.Name) -$name") }
        Write-ParameterElement $first $pos $required $true $type
    }
    $w.WriteEndElement()

    $examples = @($typeDoc.example)
    if ($examples.Count -and $examples[0]) {
        $w.WriteStartElement('command', 'examples', $cmd)
        $i = 0
        foreach ($e in $examples) {
            $i++
            $w.WriteStartElement('command', 'example', $cmd)
            $w.WriteElementString('title', $maml, "-------------------------- Example $i --------------------------")
            $code = (($e.code.InnerText -split "`r?`n") | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join "`n"
            $w.WriteElementString('code', $dev, $code)
            $w.WriteStartElement('dev', 'remarks', $dev); Para ''; $w.WriteEndElement()
            $w.WriteEndElement()
        }
        $w.WriteEndElement()
    }

    $w.WriteEndElement()
}

$w.WriteEndElement()
$w.WriteEndDocument()
$w.Dispose()

Write-Verbose "Wrote help for $($module.ExportedCmdlets.Count) cmdlets to $OutFile"
if ($missing.Count) {
    Write-Verbose ("Undocumented: " + ($missing -join '; '))
}
