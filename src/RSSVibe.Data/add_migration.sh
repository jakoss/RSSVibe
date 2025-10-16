#!/bin/bash

# Script to add a new Entity Framework migration for RSSVibe
# Usage: ./add_migration.sh <MigrationName>
# Note: Execute this script from the RSSVibe.Data directory

set -e

if [ -z "$1" ]; then
    echo "Error: Migration name is required"
    echo "Usage: ./add_migration.sh <MigrationName>"
    echo "Example: ./add_migration.sh InitialCreate"
    exit 1
fi

MIGRATION_NAME=$1
DATA_PROJECT="./RSSVibe.Data.csproj"
STARTUP_PROJECT="../RSSVibe.ApiService/RSSVibe.ApiService.csproj"
CONTEXT_NAME="RssVibeDbContext"

echo "Adding migration: $MIGRATION_NAME"
echo "Data project: $DATA_PROJECT"
echo "Startup project: $STARTUP_PROJECT"
echo "DbContext: $CONTEXT_NAME"

dotnet ef migrations add "$MIGRATION_NAME" \
    --project "$DATA_PROJECT" \
    --startup-project "$STARTUP_PROJECT" \
    --output-dir "Migrations" \
    --context "$CONTEXT_NAME"

echo ""
echo "Migration '$MIGRATION_NAME' created successfully!"
echo "Review the migration in the Migrations folder."
