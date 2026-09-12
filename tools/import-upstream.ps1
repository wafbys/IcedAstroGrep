# Imports an AstroGrep source tree into this repository, renaming namespaces and paths on the way.
#
#   pwsh tools/import-upstream.ps1 -SourcePath C:\src\astrogrep-code-r76-trunk-AstroGrep
#
# SourcePath has no default on purpose: the path to an upstream checkout is machine specific, and a
# hardcoded one made this script unusable anywhere but the machine it was written on. DestinationPath
# defaults to the repository this script lives in.
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string]$SourcePath,

	[string]$DestinationPath = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -Path $SourcePath -PathType Container)) {
	throw "The upstream source folder '$SourcePath' does not exist."
}

$srcRoot = (Resolve-Path -Path $SourcePath).Path
$dstRoot = (Resolve-Path -Path $DestinationPath).Path

function Copy-Tree($from, $to, $excludeNames) {
	New-Item -ItemType Directory -Force -Path $to | Out-Null
	Get-ChildItem -Path $from -Force | ForEach-Object {
		if ($excludeNames -contains $_.Name) { return }
		$dest = Join-Path $to $_.Name
		if ($_.PSIsContainer) {
			Copy-Tree $_.FullName $dest $excludeNames
		} else {
			Copy-Item $_.FullName $dest -Force
		}
	}
}

$skip = @(
	'bin', 'obj', 'packages', 'Installer',
	'AdminProcess', '*.csproj', 'packages.config', 'app.config', 'App.config',
	'AssemblyInfo.cs', 'AssemblyInfoCommon.cs', 'AssemblyVersionCommon.cs'
)

# Core: libAstroGrep
Copy-Tree (Join-Path $srcRoot 'libAstroGrep') (Join-Path $dstRoot 'src\IcedAstroGrep.Core') @(
	'bin','obj','Properties','libAstroGrep.csproj','packages.config','app.config','AssemblyInfo.cs'
)
# Common into Core
Copy-Tree (Join-Path $srcRoot 'AstroGrep.Common') (Join-Path $dstRoot 'src\IcedAstroGrep.Core') @(
	'bin','obj','Properties','AstroGrep.Common.csproj','packages.config','AssemblyInfo.cs'
)

# IFilter
Copy-Tree (Join-Path $srcRoot 'IFilterTextReader') (Join-Path $dstRoot 'src\IcedAstroGrep.IFilter') @(
	'bin','obj','IFilterTextReader.csproj','Properties.xlsx'
)

# App (the WinForms shell; the project is named after the shell, the assembly after the product)
$appSkip = @(
	'bin','obj','Installer','AstroGrep.csproj','packages.config','App.config','AssemblyInfo.cs',
	'DarkTheme.cs','DarkColorTable.cs','ThemeDarkMenuRenderer.cs','ThemeDarkToolStripRenderer.cs',
	'RegistryMonitor.cs'
)
Copy-Tree (Join-Path $srcRoot 'WinformsGUI') (Join-Path $dstRoot 'src\IcedAstroGrep.WinForms') $appSkip

# Remove leftover AssemblyInfo if copied
Get-ChildItem -Path $dstRoot -Recurse -Include 'AssemblyInfo.cs','packages.config','*.csproj.bak' -ErrorAction SilentlyContinue |
	Remove-Item -Force -ErrorAction SilentlyContinue

function Replace-Tokens([string]$text) {
	$t = $text
	$t = $t.Replace('libAstroGrep', '#ENGINE#')
	$t = $t.Replace('AstroGrep.Core.Theme', '#THEME#')
	$t = $t.Replace('AstroGrep.Core', '#APPCORE#')
	$t = $t.Replace('AstroGrep.Common.Logging', '#LOGGING#')
	$t = $t.Replace('AstroGrep.Common', '#ENGINE#')
	$t = $t.Replace('AstroGrep', 'IcedAstroGrep')
	$t = $t.Replace('#ENGINE#', 'IcedAstroGrep.Core')
	$t = $t.Replace('#THEME#', 'IcedAstroGrep.Theme')
	$t = $t.Replace('#APPCORE#', 'IcedAstroGrep')
	$t = $t.Replace('#LOGGING#', 'IcedAstroGrep.Core.Logging')
	$t = $t.Replace('Core.Theme.', 'Theme.')
	return $t
}

Get-ChildItem -Path (Join-Path $dstRoot 'src') -Recurse -Include *.cs,*.resx,*.config,*.xml,*.xaml |
	ForEach-Object {
		$raw = [System.IO.File]::ReadAllText($_.FullName)
		$next = Replace-Tokens $raw
		if ($next -ne $raw) {
			$utf8Bom = New-Object System.Text.UTF8Encoding $true
			[System.IO.File]::WriteAllText($_.FullName, $next, $utf8Bom)
		}
	}

Write-Output 'Import complete.'
Get-ChildItem -Path (Join-Path $dstRoot 'src') -Recurse -File | Measure-Object | ForEach-Object { "Files: $($_.Count)" }
