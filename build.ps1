<#
  Vehicle Tweaks build script.

  Drives the self-contained Roslyn compiler in tools\ rather than `dotnet build`, because the
  .NET SDK on this machine is broken (Microsoft.NETCore.App\8.0.28 is a partial install, so
  every `dotnet` invocation dies on a missing hostpolicy.dll). This path needs no SDK, no
  Visual Studio and no admin rights. Same approach as the Fumes, Hoodrich and Overspray repos.

  Usage:
    .\build.ps1                        # build to .\build\VehicleTweaks.dll
    .\build.ps1 -Deploy                # build, then install into both GTA V editions
    .\build.ps1 -Deploy -Target Legacy # ...into one of them
    .\build.ps1 -Package               # build a release zip in .\release\
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [switch]$Deploy,
    [switch]$Package,

    # Which install(s) -Deploy writes to. This is a pure SHVDN script with no asset
    # dependencies and both editions ship the identical ScriptHookVDotNet3.dll, so one build
    # runs on both.
    [ValidateSet('Legacy', 'Enhanced', 'Both')]
    [string]$Target = 'Both',

    [string]$GtaDir = 'C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V',
    [string]$EnhancedDir = 'C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V Enhanced'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$csc    = Join-Path $root 'tools\roslyn\tasks\net472\csc.exe'
$refDir = Join-Path $root 'tools\refasm\build\.NETFramework\v4.8'
$srcDir = Join-Path $root 'src\VehicleTweaks'
$outDir = Join-Path $root 'build'
$outDll = Join-Path $outDir 'VehicleTweaks.dll'

if (-not (Test-Path $csc))    { throw "Compiler missing: $csc  (see tools\README.md)" }
if (-not (Test-Path $refDir)) { throw "net48 reference assemblies missing: $refDir" }

# SHVDN is taken from whichever install is actually there. It is the same file in both.
$shvdn = $null
foreach ($dir in @($GtaDir, $EnhancedDir)) {
    $candidate = Join-Path $dir 'ScriptHookVDotNet3.dll'
    if (Test-Path $candidate) { $shvdn = $candidate; break }
}
if (-not $shvdn) { throw "ScriptHookVDotNet3.dll not found in either install." }

New-Item -ItemType Directory -Force $outDir | Out-Null

# --- references -------------------------------------------------------------
# Deliberately minimal. ZERO external runtime dependencies: only the BCL and SHVDN. No
# Newtonsoft, no LemonUI, no NativeUI -- nothing that can lose a version fight with another
# mod, because scripts\ is ONE shared assembly-resolution namespace and a third-party dll in
# there is everybody's problem, not just ours.
#
# System.Drawing is not here and Fumes needed it; this mod draws nothing. Forms is, for
# System.Windows.Forms.Keys -- the raw-key fallback in Ignition, and nothing else.
$refNames = @(
    'mscorlib.dll'
    'System.dll'
    'System.Core.dll'
    'System.Windows.Forms.dll'
)
$refs = @()
foreach ($n in $refNames) {
    $p = Join-Path $refDir $n
    if (-not (Test-Path $p)) { throw "Reference assembly missing: $p" }
    $refs += "/reference:`"$p`""
}
$refs += "/reference:`"$shvdn`""

# --- sources ----------------------------------------------------------------
$sources = Get-ChildItem $srcDir -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object { $_.FullName }

if (-not $sources) { throw "No .cs sources found under $srcDir" }

# --- compiler options -------------------------------------------------------
$opts = @(
    '/target:library'
    '/platform:x64'
    '/langversion:9.0'
    '/nologo'
    '/warnaserror-'
    '/warn:4'
    '/nostdlib+'
    '/utf8output'
    "/out:`"$outDll`""
)
if ($Configuration -eq 'Debug') {
    $opts += '/debug:portable', '/define:DEBUG;TRACE', '/optimize-'
} else {
    $opts += '/debug-', '/optimize+'
}

$rsp = Join-Path $outDir 'build.rsp'
($opts + $refs + ($sources | ForEach-Object { "`"$_`"" })) | Set-Content -Path $rsp -Encoding UTF8

Write-Host "Compiling $($sources.Count) source files -> $outDll ($Configuration)" -ForegroundColor Cyan
$sw = [Diagnostics.Stopwatch]::StartNew()
& $csc "@$rsp"
$exit = $LASTEXITCODE
$sw.Stop()

if ($exit -ne 0) { throw "Compilation failed (csc exit $exit)." }
Write-Host ("OK  {0:N0} bytes in {1:N1}s" -f (Get-Item $outDll).Length, $sw.Elapsed.TotalSeconds) -ForegroundColor Green

# --- deploy -----------------------------------------------------------------
function Read-IniKeys {
    <#
        Every "Section.Key" in an ini, so two of them can be compared by what they actually
        SET rather than by which headings they happen to have. A section-only comparison
        reports a file as current while it carries twenty dead keys inside the right headings.
    #>
    param([string]$Path)

    $section = ''
    $keys = New-Object System.Collections.Generic.List[string]

    foreach ($line in (Get-Content -LiteralPath $Path)) {
        $t = $line.Trim()

        if ($t -match '^\[(.+)\]$') { $section = $Matches[1]; continue }
        if ($t.StartsWith(';') -or $t.StartsWith('#') -or -not $t.Contains('=')) { continue }
        if (-not $section) { continue }

        $keys.Add("$section.$($t.Split('=')[0].Trim())")
    }

    return $keys
}

function Get-ReloadKey([string]$gameDir) {
    <#
        Whatever SHVDN is actually set to reload on, per install. Not assumed: the two
        installs on this machine disagree, and a wrong key in a reminder is worse than none.
    #>
    $ini = Join-Path $gameDir 'ScriptHookVDotNet.ini'
    if (-not (Test-Path $ini)) { return $null }

    $hit = Select-String -Path $ini -Pattern '^\s*ReloadKeyBinding\s*=\s*(\S+)' |
           Select-Object -First 1

    if ($hit) { return $hit.Matches[0].Groups[1].Value }
    return $null
}

function Deploy-To([string]$gameDir, [string]$label) {
    if (-not (Test-Path $gameDir)) {
        Write-Host "skip $label - not installed at $gameDir" -ForegroundColor DarkGray
        return
    }

    $scripts = Join-Path $gameDir 'scripts'
    if (-not (Test-Path $scripts)) {
        Write-Host "skip $label - no scripts folder (ScriptHookVDotNet not installed?)" -ForegroundColor Yellow
        return
    }

    Write-Host "$label -> $scripts" -ForegroundColor Cyan

    # THE COPY IS ATTEMPTED, NOT PRE-REFUSED.
    #
    # Inherited from Fumes, where this used to check whether the game was running and give up
    # if it was -- a guess dressed up as a rule. SHVDN SHADOW-COPIES script assemblies into the
    # .NET download cache and runs them from there, so the dll sitting in scripts\ is very often
    # not locked at all. Refusing on the strength of a process name meant closing the game for
    # every change for no reason.
    #
    # So it tries. A genuine lock throws, and that is reported for what it is.
    $locked = $false
    try {
        Copy-Item $outDll $scripts -Force -ErrorAction Stop
    } catch {
        $locked = $true
        Write-Host "  LOCKED VehicleTweaks.dll is in use." -ForegroundColor Yellow
        Write-Host "         Close the game and re-run to update the dll." -ForegroundColor DarkGray
    }

    $pdb = Join-Path $outDir 'VehicleTweaks.pdb'
    if (-not $locked -and (Test-Path $pdb)) {
        try { Copy-Item $pdb $scripts -Force -ErrorAction Stop } catch { }
    }

    if (-not $locked -and (Get-Process GTA5, GTA5_Enhanced -ErrorAction SilentlyContinue)) {
        # The reload key is read from SHVDN's own ini rather than assumed. The two installs
        # on this machine do not agree on it -- Legacy is Pause, Enhanced is Insert -- so a
        # hardcoded reminder would be wrong half the time.
        $key = Get-ReloadKey $gameDir
        $named = if ($key) { $key } else { "the SHVDN reload key" }
        Write-Host "  LIVE   dll replaced while the game runs - press $named in game to reload." -ForegroundColor Green
    }

    # The ini is never overwritten: it is the file players hand-edit. Say what is missing from
    # it instead, so a new setting is not silently on its default forever.
    #
    # This is the whole of the data handling now. Fumes had a file-by-file guard here because it
    # shipped a station list and wrote a save beside it; this mod ships one ini and writes one
    # log, so there is nothing else that a deploy could destroy.
    $iniSrc = Join-Path $root 'VehicleTweaks.ini'
    $iniDst = Join-Path $scripts 'VehicleTweaks.ini'

    if (-not (Test-Path $iniSrc)) { return }

    if (-not (Test-Path $iniDst)) {
        Copy-Item $iniSrc $iniDst
        Write-Host "  new    VehicleTweaks.ini" -ForegroundColor DarkGray
        return
    }

    $srcKeys = Read-IniKeys $iniSrc
    $dstKeys = Read-IniKeys $iniDst

    $absent = $srcKeys | Where-Object { $dstKeys -notcontains $_ }
    $extra  = $dstKeys | Where-Object { $srcKeys -notcontains $_ }

    if ($absent) {
        Write-Host "  STALE  VehicleTweaks.ini is missing $($absent.Count) setting(s):" -ForegroundColor Yellow
        Write-Host "         $($absent -join ', ')" -ForegroundColor DarkGray
        Write-Host "         Defaults apply until they are added." -ForegroundColor DarkGray
    }
    if ($extra) {
        Write-Host "  STALE  VehicleTweaks.ini has $($extra.Count) setting(s) nothing reads:" -ForegroundColor Yellow
        Write-Host "         $($extra -join ', ')" -ForegroundColor DarkGray
    }
    if (-not $absent -and -not $extra) {
        Write-Host "  keep   VehicleTweaks.ini ($($srcKeys.Count) settings, all current)" -ForegroundColor DarkGray
    }
}

if ($Deploy) {
    # No process check at all. Deploy-To attempts the copy and reports a real lock if it hits
    # one; a running game is usually not a reason to stop, because SHVDN runs scripts out of a
    # shadow copy rather than out of scripts\.
    if ($Target -in 'Legacy', 'Both')   { Deploy-To $GtaDir      'Legacy' }
    if ($Target -in 'Enhanced', 'Both') { Deploy-To $EnhancedDir 'Enhanced' }

    Write-Host "Deploy complete." -ForegroundColor Green
}

# --- packaging ---------------------------------------------------------------
# A zip that merges straight over the GTA V folder, because that is the one install
# instruction nobody gets wrong. Built only from the repo -- never from the game folder, or a
# release ships whatever this machine happens to be testing with.
if ($Package) {
    $version = (Select-String -Path (Join-Path $root 'src\VehicleTweaks\Core\Log.cs') `
                              -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value

    $relDir = Join-Path $root 'release'
    $stage  = Join-Path $relDir "VehicleTweaks-$version"
    $zip    = Join-Path $relDir "VehicleTweaks-$version.zip"

    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

    $scripts = Join-Path $stage 'scripts'
    New-Item -ItemType Directory -Force -Path $scripts | Out-Null

    Copy-Item $outDll (Join-Path $scripts 'VehicleTweaks.dll')
    Copy-Item (Join-Path $root 'VehicleTweaks.ini') (Join-Path $scripts 'VehicleTweaks.ini')

    foreach ($doc in @('README.txt', 'CHANGES.txt')) {
        $p = Join-Path $relDir $doc
        if (Test-Path $p) { Copy-Item $p $stage }
    }

    # Belt and braces: a log in a release zip is this machine's debugging session shipped to
    # somebody else, and it lands in the folder their own log is about to be written to.
    Get-ChildItem $stage -Recurse -Include '*.log', '*.log.1', '*.bak' |
        ForEach-Object { Remove-Item $_.FullName -Force }

    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

    $files = (Get-ChildItem $stage -Recurse -File).Count
    $size  = [math]::Round((Get-Item $zip).Length / 1KB)

    Write-Host ""
    Write-Host "Packaged  $zip" -ForegroundColor Green
    Write-Host "          $files files, $size KB, version $version"
}
