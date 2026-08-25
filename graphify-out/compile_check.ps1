$ErrorActionPreference = "Stop"
$proj = "D:\Production\wrong direction"
$u = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Data"
$dotnet = "$u\DotNetSdk\dotnet.exe"
$csc = "$u\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"

$refs = @()
$refs += Get-ChildItem "$u\Managed\UnityEngine\*.dll" | ForEach-Object { "-r:`"$($_.FullName)`"" }
$refs += "-r:`"$u\NetStandard\ref\2.1.0\netstandard.dll`""
$refs += "-r:`"$u\NetStandard\compat\2.1.0\shims\netfx\mscorlib.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\UnityEngine.UI.dll`""
$refs += "-r:`"$proj\Library\ScriptAssemblies\Unity.TextMeshPro.dll`""
$refs += "-r:`"$proj\Assets\Plugins\Demigiant\DOTween\DOTween.dll`""
# LevelPlay + Unity Services (LevelPlayAdProvider): runtime assemblies only,
# never the *.Editor ones — they pull in editor-only types this check can't see.
$refs += "-r:`"$proj\Library\ScriptAssemblies\Unity.LevelPlay.dll`""
$refs += Get-ChildItem "$proj\Library\ScriptAssemblies\Unity.Services.*.dll" |
    Where-Object { $_.Name -notmatch '\.Editor\.dll$' } |
    ForEach-Object { "-r:`"$($_.FullName)`"" }

$sources = @()
$sources += Get-ChildItem "$proj\Assets\Scripts" -Recurse -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
$sources += "`"$proj\Assets\Editor\BuildMainScene.cs`""
$sources += Get-ChildItem "$proj\Assets\Plugins\Demigiant\DOTween\Modules\*.cs" | ForEach-Object { "`"$($_.FullName)`"" }

$rsp = "$env:TEMP\wd_compile.rsp"
$args = @("-nologo", "-target:library", "-nowarn:0168,0219,0414,0649", "-define:UNITY_EDITOR;UNITY_2021_1_OR_NEWER;UNITY_6000", "-out:`"$env:TEMP\wd_check.dll`"") + $refs + $sources
$args | Set-Content $rsp -Encoding utf8

& $dotnet $csc "@$rsp"
if ($LASTEXITCODE -eq 0) { Write-Output "COMPILE OK ($($sources.Count) sources)" } else { Write-Output "COMPILE FAILED" }
exit $LASTEXITCODE
