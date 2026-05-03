# generate-installer.ps1
# Generates installer\AppFiles.wxs from publish\win-x64, then builds the MSI.
# Run from the repo root: .\installer\generate-installer.ps1

$publishDir = (Resolve-Path "$PSScriptRoot\..\publish\win-x64").Path
$outWxs     = "$PSScriptRoot\AppFiles.wxs"
$installRoot = "INSTALLFOLDER"

function New-Guid { [System.Guid]::NewGuid().ToString().ToUpper() }

$md5 = [System.Security.Cryptography.MD5]::Create()
function Get-SafeId($path, $base) {
    $rel = $path.Substring($base.Length).TrimStart('\','/').ToLowerInvariant()
    $hashBytes = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($rel))
    $hash = [BitConverter]::ToString($hashBytes) -replace '-'
    "ID_" + $hash
}

$allFiles = Get-ChildItem $publishDir -Recurse -File |
            Where-Object { $_.Name -ne "crash.txt" -and $_.Name -ne "ConversionApp.pdb" }

$allDirs  = Get-ChildItem $publishDir -Recurse -Directory

$sb = [System.Text.StringBuilder]::new()
$null = $sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
$null = $sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
$null = $sb.AppendLine('  <Fragment>')

# ── Directory tree ──────────────────────────────────────────────────────────
$null = $sb.AppendLine("  <DirectoryRef Id=`"$installRoot`">")
foreach ($dir in $allDirs) {
    $rel  = $dir.FullName.Substring($publishDir.Length).TrimStart('\')
    $parts = $rel -split '\\'
    $indent = "      " + ("  " * ($parts.Count - 1))
    $dirId  = "D_" + (Get-SafeId $dir.FullName $publishDir).Substring(3)
    $null = $sb.AppendLine("$indent<Directory Id=`"$dirId`" Name=`"$($dir.Name)`">")
    $null = $sb.AppendLine("$indent</Directory>")
}
$null = $sb.AppendLine("  </DirectoryRef>")

# ── ComponentGroup with one Component per file ───────────────────────────────
$null = $sb.AppendLine('  <ComponentGroup Id="AppFiles">')
foreach ($f in $allFiles) {
    $rel    = $f.FullName.Substring($publishDir.Length).TrimStart('\')
    $fileId = Get-SafeId $f.FullName $publishDir
    $compId = "C" + $fileId.Substring(1)

    # Determine parent directory ref
    $parentRel = Split-Path $rel -Parent
    $dirRef = if ($parentRel -eq "") { $installRoot } else { "D_" + (Get-SafeId "$publishDir\$parentRel" $publishDir).Substring(3) }

    $guid = New-Guid
    $null = $sb.AppendLine("    <Component Id=`"$compId`" Guid=`"$guid`" Directory=`"$dirRef`">")
    $null = $sb.AppendLine("      <File Id=`"$fileId`" Source=`"..\publish\win-x64\$rel`" KeyPath=`"yes`" />")
    $null = $sb.AppendLine("    </Component>")
}
$null = $sb.AppendLine('  </ComponentGroup>')
$null = $sb.AppendLine('  </Fragment>')
$null = $sb.AppendLine('</Wix>')

$sb.ToString() | Out-File $outWxs -Encoding UTF8
Write-Host "✔ Generated $outWxs with $($allFiles.Count) files."

# ── Build the MSI ─────────────────────────────────────────────────────────────
Write-Host "Building MSI..."
Push-Location $PSScriptRoot
dotnet build EezyPro.wixproj -c Release -o out
Pop-Location
