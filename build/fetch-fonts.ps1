<#
.SYNOPSIS
  Downloads the Noto Sans fonts (SIL Open Font License 1.1) used by the TsuFiles UI.

.DESCRIPTION
  Fonts are not committed to git. Run this once after cloning, and CI runs it before publishing.
  Only Noto Sans (Latin, Vietnamese) is embedded: TsuFiles ships Vietnamese and English (phiếu TG1, 26/11/2026);
  other scripts in file names use system fonts (Segoe UI, Malgun Gothic, Microsoft YaHei, ...).
  Without the fonts the app still works and falls back to system fonts.

  Static font files are used because Avalonia does not support variable fonts.
  Every file is pinned to a tag/commit and verified with SHA-256.

.EXAMPLE
  pwsh build/fetch-fonts.ps1
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\src\TsuFiles.App\Assets\Fonts')
)

$ErrorActionPreference = 'Stop'
$noto = 'https://raw.githubusercontent.com/notofonts/notofonts.github.io/28b15b4b43b7bed62b5cf6e6b0b5ff5846270535/fonts'
$latin = "$noto/NotoSans/hinted/ttf"

# The same files on TsuFiles's download mirror (R2 bucket filee-downloads), kept under their SHA-256 so that a new version
# never replaces one an older branch pins. Copy a new or bumped file there too:
#   npx wrangler r2 object put filee-downloads/fonts/<SHA-256>/<name> --file <file> --remote
$mirror = 'https://dl.filee.sh/fonts'

# File name => (URL, SHA-256). Update both together when bumping a version.
$fonts = [ordered]@{
    'NotoSans-Regular.ttf'           = @("$latin/NotoSans-Regular.ttf", '478C558EA716033CD60C03438F628DFA75694DCF6B5F6D505A2F05FD2B4F3823')
    'NotoSans-Medium.ttf'            = @("$latin/NotoSans-Medium.ttf", '635D93D1131D791F2576DE90B3BB0F7CDF61929906E8420A61B5F7F8E76420BB')
    'NotoSans-Bold.ttf'              = @("$latin/NotoSans-Bold.ttf", '1DF075A380FC7CB898ACF64C1F7B3B4DD780DE3CAA860178BF929DE35817A913')
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
# Fonts an older version of this script fetched (Arabic, Indic, Thai, CJK) would still be embedded: remove them.
Get-ChildItem (Join-Path $Destination '*') -File -Include '*.ttf', '*.otf' -ErrorAction SilentlyContinue |
    Where-Object { -not $fonts.Contains($_.Name) } | Remove-Item -Force

foreach ($name in $fonts.Keys) {
    $url, $expected = $fonts[$name]
    $target = Join-Path $Destination $name

    if (Test-Path $target) {
        $actual = (Get-FileHash $target -Algorithm SHA256).Hash
        if ($actual -eq $expected) {
            Write-Host "ok   $name (cached)"
            continue
        }
    }

    Write-Host "get  $name"
    # A source can fail for a moment (a 503 has stopped whole CI jobs): once more, then the mirror. The checksum decides.
    $actual = $null
    foreach ($source in $url, $url, "$mirror/$expected/$name") {
        try {
            Invoke-WebRequest -Uri $source -OutFile $target -UseBasicParsing
            $actual = (Get-FileHash $target -Algorithm SHA256).Hash
            if ($actual -eq $expected) {
                break
            }
            Write-Warning "$name from $source has the checksum $actual."
        }
        catch {
            Write-Warning "$name from ${source}: $($_.Exception.Message)"
        }
        Start-Sleep -Seconds 2
    }
    if ($actual -ne $expected) {
        Remove-Item $target -ErrorAction SilentlyContinue
        throw "No source gave $name with the checksum $expected."
    }
}

Write-Host "Fonts ready in $Destination"
