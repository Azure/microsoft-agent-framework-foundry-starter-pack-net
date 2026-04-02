#!/bin/bash

REPOSITORY_ROOT=$(git rev-parse --show-toplevel)

dotnet user-secrets \
    --project "$REPOSITORY_ROOT/src/MafStarterPack.AppHost/MafStarterPack.AppHost.csproj" list | \
    while IFS=' = ' read -r key value; do
        dotnet user-secrets \
            --project "$REPOSITORY_ROOT/src/MafStarterPack.AppHost/MafStarterPack.AppHost.csproj" \
            remove "$key"
    done
