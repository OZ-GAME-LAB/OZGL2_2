param(
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$BaseCommit = '0ebde218b3b864fb7e91ed48bad8a06c81e1a795',
    [string]$OutputName = ('UiReview-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
)

# Read-only for the working tree and real Git index. Outputs and the temporary
# index live under ignored Logs; only Git blob/tree objects are added to .git.
$ErrorActionPreference = 'Stop'
if ($BaseCommit -notmatch '^[0-9a-f]{40}$') { throw 'Use an exact reviewed base commit.' }
if ($OutputName -notmatch '^[a-zA-Z0-9-]+$') { throw 'OutputName must be a simple directory name.' }
if ($env:GIT_INDEX_FILE) { throw 'An alternate Git index is already configured.' }
$repoPath = (Resolve-Path -LiteralPath $Repository).Path
if (!(Test-Path -LiteralPath (Join-Path $repoPath 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Not the expected Unity project.'
}
$outputPath = Join-Path $repoPath ('Logs/' + $OutputName)
if (Test-Path -LiteralPath $outputPath) { throw 'Output exists; preserve it and choose a new name.' }

function Invoke-ReviewGit {
    param([string[]]$GitArguments)
    $result = & git -c core.quotepath=false -c core.safecrlf=false -C $repoPath @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "git command failed: $($GitArguments[0])" }
    return $result
}

$resolvedBase = (Invoke-ReviewGit @('rev-parse', '--verify', ($BaseCommit + '^{commit}'))).Trim()
$originalHead = (Invoke-ReviewGit @('rev-parse', 'HEAD')).Trim()
$originalBranch = (Invoke-ReviewGit @('branch', '--show-current')).Trim()
$indexPath = (Invoke-ReviewGit @('rev-parse', '--path-format=absolute', '--git-path', 'index')).Trim()
$originalIndexHash = (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash
$beforeStatus = @(Invoke-ReviewGit @('status', '--porcelain=v1', '--untracked-files=all'))
New-Item -ItemType Directory -Path $outputPath | Out-Null

# Back up every current modified/untracked source file, including the excluded
# shared font. Deleted paths are recorded, not recreated. No user file is moved.
$wipPaths = @(Invoke-ReviewGit @('ls-files', '--modified', '--deleted', '--others', '--exclude-standard') | Sort-Object -Unique)
$backupManifest = foreach ($relative in $wipPaths) {
    $source = Join-Path $repoPath $relative
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        $backup = Join-Path $outputPath ('WorkingBackup/' + $relative)
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $backup
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne $hash) {
            throw "Backup mismatch: $relative"
        }
        [pscustomobject]@{ Path = $relative; Exists = $true; Sha256 = $hash }
    } else {
        [pscustomobject]@{ Path = $relative; Exists = $false; Sha256 = '' }
    }
}
$backupManifest | Export-Csv -LiteralPath (Join-Path $outputPath 'working-backup.csv') -NoTypeInformation -Encoding utf8
Invoke-ReviewGit @('diff', '--binary', '--full-index', ('--output=' + (Join-Path $outputPath 'original-working.patch'))) | Out-Null

$scopes = @(
    'Assets/Scripts/UI', 'Assets/Scripts/UI.meta',
    'Assets/Data/UI', 'Assets/Data/UI.meta',
    'Assets/Prefabs/UI', 'Assets/Prefabs/UI.meta',
    'Assets/Scenes/UI', 'Assets/Scenes/UI.meta',
    ':(glob)Assets/Scenes/Test/Mvp*.unity', ':(glob)Assets/Scenes/Test/Mvp*.unity.meta',
    'Docs/UI', 'Docs/AI/UnityProjectContext.md', 'Tools/UI'
)
$tree = $null
try {
    $env:GIT_INDEX_FILE = Join-Path $outputPath 'candidate.index'
    Invoke-ReviewGit @('read-tree', $resolvedBase) | Out-Null
    Invoke-ReviewGit (@('add', '-A', '--') + $scopes) | Out-Null
    # Unity emits trailing spaces for empty YAML values and Markdown can use
    # two trailing spaces intentionally. Keep the complete diagnostic, but
    # enforce clean whitespace on the hand-written C#/PowerShell code only.
    $whitespace = @(& git -c core.safecrlf=false -C $repoPath diff --cached --check $resolvedBase)
    $whitespace | Export-Clixml -LiteralPath (Join-Path $outputPath 'whitespace-check.xml')
    Invoke-ReviewGit @('diff', '--cached', '--check', $resolvedBase, '--', '*.cs', '*.ps1') | Out-Null
    $changes = @(Invoke-ReviewGit @('diff', '--cached', '--name-status', $resolvedBase))
    $manifest = foreach ($line in $changes) {
        $parts = $line -split "`t"
        $status = $parts[0]; $path = $parts[1]
        if ($status -notin @('A', 'M')) { throw "Unexpected deletion/rename in UI candidate: $line" }
        $group = if ($path -match '^Assets/Scripts/UI(?:/|\.meta$)') { 'UI code and tests' }
            elseif ($path -match '^Assets/(Data|Prefabs|Scenes)/UI(?:/|\.meta$)') { 'UI assets' }
            elseif ($path -match '^Assets/Scenes/Test/Mvp[^/]*\.unity(?:\.meta)?$') { 'UI test scenes' }
            elseif ($path -match '^Docs/(UI/|AI/UnityProjectContext\.md$)') { 'UI documentation' }
            elseif ($path -match '^Tools/UI/') { 'UI review tooling' }
            else { throw "Non-UI file in candidate: $path" }
        [pscustomobject]@{ Status = $status; Group = $group; Path = $path }
    }
    $manifest | Export-Csv -LiteralPath (Join-Path $outputPath 'ui-changes.csv') -NoTypeInformation -Encoding utf8
    Invoke-ReviewGit @('diff', '--cached', '--binary', '--full-index', '--no-ext-diff',
        ('--output=' + (Join-Path $outputPath 'ui-only.patch')), $resolvedBase) | Out-Null
    $tree = (Invoke-ReviewGit @('write-tree')).Trim()
    Invoke-ReviewGit @('archive', '--format=zip', ('--output=' + (Join-Path $outputPath 'candidate-project.zip')), $tree) | Out-Null
} finally {
    Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue
}

# Verify originals after packaging, not just before. The real index, HEAD,
# branch, working files and untracked status must remain exactly as they were.
if ((Invoke-ReviewGit @('rev-parse', 'HEAD')).Trim() -ne $originalHead -or
    (Invoke-ReviewGit @('branch', '--show-current')).Trim() -ne $originalBranch -or
    (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash -ne $originalIndexHash) {
    throw 'Git state changed during packaging; inspect before proceeding.'
}
$afterStatus = @(Invoke-ReviewGit @('status', '--porcelain=v1', '--untracked-files=all'))
if (Compare-Object $beforeStatus $afterStatus) { throw 'Working status changed during packaging.' }
foreach ($entry in $backupManifest) {
    $current = Join-Path $repoPath $entry.Path
    if ($entry.Exists) {
        if (!(Test-Path -LiteralPath $current) -or
            (Get-FileHash -LiteralPath $current -Algorithm SHA256).Hash -ne $entry.Sha256) {
            throw "Working source changed: $($entry.Path)"
        }
    } elseif (Test-Path -LiteralPath $current) { throw "Deleted path unexpectedly restored: $($entry.Path)" }
}
$summary = [pscustomobject]@{
    Repository = $repoPath; Branch = $originalBranch; Head = $originalHead
    BaseCommit = $resolvedBase; CandidateTree = $tree
    ChangedUiFiles = $manifest.Count; BackedUpPaths = $backupManifest.Count
    NonUiChanges = 0; SharedFontExcluded = $true; WorkingStatePreserved = $true
    Output = $outputPath
}
$summary | Export-Clixml -LiteralPath (Join-Path $outputPath 'summary.xml')
$summary | ConvertTo-Json
$manifest | Group-Object Group | Select-Object Name, Count | Format-Table -AutoSize
