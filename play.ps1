<#
  play.ps1 - one command to play against YOUR AI.

    .\play.ps1                 train if needed (130000 self-play games), then start the game
    .\play.ps1 -Games 300000   a different amount of training
    .\play.ps1 -Train          play another -Games games on top of the existing training, then start

  What it does, in order:
    1. looks for trained weights in the project's own models folder (models\latest.npz) and counts the games
       they were trained on (first run: there are none, so the AI starts from zero)
    2. if there are fewer than -Games games (or none at all) it trains the missing ones
       (resumes an interrupted training automatically)
    3. converts the weights for the game (BackgammonByHoratiu\Ai\net.bgnn)
    4. starts the game

  Options: -AiProject <folder of backgammon_ai_v6>  -Models <folder for the .npz files>  -Workers <cores>
#>
param(
    [string]$AiProject = "",
    [string]$Models = "",
    [int]$Games = 130000,
    [int]$Workers = 0,
    [switch]$Train
)

$ErrorActionPreference = "Stop"
$repo = $PSScriptRoot
$parent = Split-Path $repo -Parent

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host ""; Write-Host "ERROR: $text" -ForegroundColor Red; exit 1 }

# ---- tools -------------------------------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail "The .NET SDK was not found. Install .NET 10 SDK, then open a NEW terminal."
}
$python = $null
$pyArgs = @()
if (Get-Command python -ErrorAction SilentlyContinue) {
    $python = "python"
} elseif (Get-Command py -ErrorAction SilentlyContinue) {
    $python = "py"
    $pyArgs = @("-3")
}
if (-not $python) {
    Fail "Python was not found. Run this from the Anaconda Prompt / the terminal where 'python' works."
}

# ---- where is the AI project, where are the models ----------------------------------------
if (-not $AiProject) {
    foreach ($candidate in @((Join-Path $parent "backgammon_ai_v6"), (Join-Path $repo "backgammon_ai_v6"),
                             (Join-Path $parent "backgammon_ai_v5"), (Join-Path $repo "backgammon_ai_v5"))) {
        if (Test-Path (Join-Path $candidate "train.py")) { $AiProject = $candidate; break }
    }
}
if (-not $AiProject -or -not (Test-Path (Join-Path $AiProject "train.py"))) {
    Fail "Could not find the AI project (the folder with train.py). Run again with -AiProject <folder>."
}
if (-not $Models) { $Models = Join-Path $repo "models" }
New-Item -ItemType Directory -Force -Path $Models | Out-Null

$latest = Join-Path $Models "latest.npz"
$net = Join-Path $repo "BackgammonByHoratiu\Ai\net.bgnn"

Write-Host "AI project : $AiProject"
Write-Host "Models     : $Models"

# ---- 1) how much training exists ----------------------------------------------------------
$done = 0
if (Test-Path $latest) {
    $readGames = "import numpy as np, sys; print(int(np.load(sys.argv[1])['games']))"
    $done = [int](& $python @pyArgs -c $readGames $latest)
    if ($LASTEXITCODE -ne 0) { Fail "Could not read $latest" }
}
Write-Host "Trained on : $done games"

# ---- 2) train what is missing --------------------------------------------------------------
if ($Train) { $toPlay = $Games } else { $toPlay = $Games - $done }

if ($toPlay -gt 0) {
    Step "Training: $toPlay self-play games (this can take a while; progress is printed, Ctrl+C is safe - run again to continue)"
    $trainArgs = @("train.py", "--games", [string]$toPlay, "--out", $Models, "--seed", [string]$done)
    if ($Workers -gt 0) { $trainArgs += @("--workers", [string]$Workers) }
    if ($done -gt 0) { $trainArgs += @("--resume", $latest) }

    Push-Location $AiProject
    try {
        & $python @pyArgs @trainArgs
        if ($LASTEXITCODE -ne 0) { Fail "Training failed (see the messages above)." }
    } finally {
        Pop-Location
    }
}
if (-not (Test-Path $latest)) { Fail "No trained network found at $latest" }

# ---- 3) export for the game ------------------------------------------------------------------
if (-not (Test-Path $net) -or ((Get-Item $latest).LastWriteTime -gt (Get-Item $net).LastWriteTime)) {
    Step "Converting the weights for the game"
    & $python @pyArgs (Join-Path $repo "tools\export_weights.py") $latest $net
    if ($LASTEXITCODE -ne 0) { Fail "Could not convert the weights (see the messages above)." }
}

# ---- 4) play ---------------------------------------------------------------------------------
Step "Starting the game"
Set-Location $repo
dotnet run --project (Join-Path $repo "BackgammonByHoratiu")
exit $LASTEXITCODE
