$ErrorActionPreference = "Stop"
$proj = "D:\Production\wrong direction"
$u = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Data"
$dotnet = "$u\DotNetSdk\dotnet.exe"
$csc = "$u\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"

$gma = Get-ChildItem "$proj\Library\PackageCache\com.google.ads.mobile*" -Directory | Select-Object -First 1

$refs = @()
$refs += Get-ChildItem "$u\Managed\UnityEngine\*.dll" | ForEach-Object { "-r:`"$($_.FullName)`"" }
$refs += "-r:`"$u\NetStandard\ref\2.1.0\netstandard.dll`""
$refs += "-r:`"$u\NetStandard\compat\2.1.0\shims\netfx\mscorlib.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\UnityEngine.UI.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\Unity.TextMeshPro.dll`""
$refs += "-r:`"$proj\Assets\Plugins\Demigiant\DOTween\DOTween.dll`""
$refs += Get-ChildItem "$($gma.FullName)\GoogleMobileAds\GoogleMobileAds*.dll" | Where-Object { $_.Name -notmatch "iOS|Android" } | ForEach-Object { "-r:`"$($_.FullName)`"" }

$sources = @()
$sources += Get-ChildItem "$proj\Assets\Scripts" -Recurse -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
$sources += "`"$proj\Assets\Editor\BuildMainScene.cs`""
$sources += Get-ChildItem "$proj\Assets\Plugins\Demigiant\DOTween\Modules\*.cs" | ForEach-Object { "`"$($_.FullName)`"" }

$rsp = "$env:TEMP\wd_compile_admob.rsp"
$args = @("-nologo", "-target:library", "-nowarn:0168,0219,0414,0649", "-define:UNITY_EDITOR;UNITY_2021_1_OR_NEWER;UNITY_6000;GOOGLE_MOBILE_ADS", "-out:`"$env:TEMP\wd_check_admob.dll`"") + $refs + $sources
$args | Set-Content $rsp -Encoding utf8

& $dotnet $csc "@$rsp"
if ($LASTEXITCODE -eq 0) { Write-Output "COMPILE OK WITH GMA ($($sources.Count) sources)" } else { Write-Output "COMPILE FAILED" }
exit $LASTEXITCODE
