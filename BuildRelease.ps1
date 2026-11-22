
$ErrorActionPreference = "Stop"
$GameDir = "E:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
$ModName = "ALPHANET"
$ModDir = Join-Path $GameDir "mods\$ModName"
$StageDir = Join-Path $PSScriptRoot "release_temp"
$ZipPath = Join-Path $PSScriptRoot "$ModName.zip"

Write-Host "================================"
Write-Host "Building $ModName..."
Write-Host "================================"

dotnet build "$PSScriptRoot\$ModName.csproj" `
    -c Release `
    /p:RunPckExport=true

if ($LASTEXITCODE -ne 0) {
    throw "Build failed!"
}

$Files = @(
    "$ModName.pck",
    "$ModName.dll",
    "$ModName.json"
)

foreach ($File in $Files) {
    $Path = Join-Path $ModDir $File

    if (!(Test-Path $Path)) {
        throw "Missing release file: $Path"
    }
}

if (Test-Path $StageDir) {
    Remove-Item $StageDir -Recurse -Force
}

New-Item -ItemType Directory -Path "$StageDir\$ModName" -Force | Out-Null

foreach ($File in $Files) {
    Copy-Item (Join-Path $ModDir $File) "$StageDir\$ModName\"
}

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive `
    -Path "$StageDir\$ModName" `
    -DestinationPath $ZipPath `
    -CompressionLevel Optimal

Remove-Item $StageDir -Recurse -Force

Write-Host "================================"
Write-Host "Release package created!"
Write-Host $ZipPath
Write-Host "================================"
