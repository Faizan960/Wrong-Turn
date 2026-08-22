$ErrorActionPreference = "Stop"
$proj = "D:\Production\wrong direction"
$u = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Data"
$dotnet = "$u\DotNetSdk\dotnet.exe"
$csc = "$u\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"

# DEVICE path: no UNITY_EDITOR, with UNITY_ANDROID + UNITY_ADS. This exercises
# the AdsManager #else branch (selects LevelPlayAdProvider) AND the legacy
# UnityAdsProvider (#if UNITY_ADS). References both LevelPlay and the legacy
# Advertisements assembly.
$refs = @()
$refs += Get-ChildItem "$u\Managed\UnityEngine\*.dll" | ForEach-Object { "-r:`"$($_.FullName)`"" }
$refs += "-r:`"$u\NetStandard\ref\2.1.0\netstandard.dll`""
$refs += "-r:`"$u\NetStandard\compat\2.1.0\shims\netfx\mscorlib.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\UnityEngine.UI.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\Unity.TextMeshPro.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\Unity.LevelPlay.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\UnityEngine.Advertisements.dll`""
$refs += "-r:`"$proj\Assets\Plugins\Demigiant\DOTween\DOTween.dll`""

$sources = @()
$sources += Get-ChildItem "$proj\Assets\Scripts" -Recurse -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
$sources += Get-ChildItem "$proj\Assets\Plugins\Demigiant\DOTween\Modules\*.cs" | ForEach-Object { "`"$($_.FullName)`"" }

$rsp = "$env:TEMP\wd_compile_device.rsp"
$args = @("-nologo", "-target:library", "-nowarn:0168,0219,0414,0649", "-define:UNITY_ANDROID;UNITY_ADS;UNITY_2021_1_OR_NEWER;UNITY_6000", "-out:`"$env:TEMP\wd_check_device.dll`"") + $refs + $sources
$args | Set-Content $rsp -Encoding utf8

& $dotnet $csc "@$rsp"
if ($LASTEXITCODE -eq 0) { Write-Output "COMPILE OK (Device path, $($sources.Count) sources)" } else { Write-Output "COMPILE FAILED" }
exit $LASTEXITCODE
