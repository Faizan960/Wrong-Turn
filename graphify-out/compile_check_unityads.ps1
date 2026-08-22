$ErrorActionPreference = "Stop"
$proj = "D:\Production\wrong direction"
$u = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Data"
$dotnet = "$u\DotNetSdk\dotnet.exe"
$csc = "$u\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"

# Verifies the generic ad layer + game after the AdMob -> Unity Ads swap,
# via the EDITOR (Mock provider) code path. UnityAdsProvider is #if UNITY_ADS
# and is compiled by Unity once the com.unity.ads package resolves.
$refs = @()
$refs += Get-ChildItem "$u\Managed\UnityEngine\*.dll" | ForEach-Object { "-r:`"$($_.FullName)`"" }
$refs += "-r:`"$u\NetStandard\ref\2.1.0\netstandard.dll`""
$refs += "-r:`"$u\NetStandard\compat\2.1.0\shims\netfx\mscorlib.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\UnityEngine.UI.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\Unity.TextMeshPro.dll`""
$refs += "-r:`"$proj\Assets\Plugins\Demigiant\DOTween\DOTween.dll`""

$sources = @()
$sources += Get-ChildItem "$proj\Assets\Scripts" -Recurse -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
$sources += "`"$proj\Assets\Editor\BuildMainScene.cs`""
$sources += Get-ChildItem "$proj\Assets\Plugins\Demigiant\DOTween\Modules\*.cs" | ForEach-Object { "`"$($_.FullName)`"" }

$rsp = "$env:TEMP\wd_compile_unityads.rsp"
$args = @("-nologo", "-target:library", "-nowarn:0168,0219,0414,0649", "-define:UNITY_EDITOR;UNITY_2021_1_OR_NEWER;UNITY_6000", "-out:`"$env:TEMP\wd_check_unityads.dll`"") + $refs + $sources
$args | Set-Content $rsp -Encoding utf8

& $dotnet $csc "@$rsp"
if ($LASTEXITCODE -eq 0) { Write-Output "COMPILE OK (Mock path, $($sources.Count) sources)" } else { Write-Output "COMPILE FAILED" }
exit $LASTEXITCODE
