param()
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    if (!(Test-Path (Join-Path $framework 'csc.exe'))) {
        $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    }
    $compiler = Join-Path $framework 'csc.exe'
    $wpf = Join-Path $framework 'WPF'
    New-Item -ItemType Directory -Force dist | Out-Null
    & $compiler /nologo /optimize+ /codepage:65001 /target:winexe /out:dist\OrbitLauncher.exe /resource:resources\zh.tsv,zh.tsv /resource:resources\guide.html,guide.html "/r:$wpf\WindowsBase.dll" "/r:$wpf\UIAutomationClient.dll" "/r:$wpf\UIAutomationTypes.dll" src\Launcher.cs src\ChineseLayer.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    Get-FileHash -LiteralPath 'dist\OrbitLauncher.exe' -Algorithm SHA256
} finally { Pop-Location }
