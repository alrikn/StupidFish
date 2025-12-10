#!/bin/bash

# Convenience script to run batch mode with 200 parallel games
# Usage: ./run_batch.sh [number_of_games] [parallel_limit]

GAMES=${1:-200}
PARALLEL=${2:-200}

DOTNET="dotnet"

echo "Running $GAMES games with max $PARALLEL in parallel..."
echo "Using: $($DOTNET --version)"
echo ""

cd "$(dirname "$0")/Chess-Challenge"
$DOTNET build
$DOTNET run -c CI -- --batch --games=$GAMES --parallel=$PARALLEL
