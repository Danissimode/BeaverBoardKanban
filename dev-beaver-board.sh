#!/bin/bash
# Beaver Board Development Mode
# Runs with hot-reload for development

cd "$(dirname "${BASH_SOURCE[0]}")"

echo "🚀 Starting Beaver Board in development mode..."
echo "   Hot reload enabled, changes will apply automatically"
echo ""
cd KittyClaw.Web && dotnet watch --non-interactive --project KittyClaw.Web.csproj