#!/bin/bash
# Beaver Board Launcher Script
# Usage: ./run-beaver-board.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_PATH="$SCRIPT_DIR/packaging/macos/BeaverBoard.app"

# Check if app exists
if [ ! -d "$APP_PATH" ]; then
    echo "❌ BeaverBoard.app not found at: $APP_PATH"
    echo "   Please build the app first or run from the project directory."
    exit 1
fi

# Check if already running
if pgrep -f "KittyClaw.Web" > /dev/null 2>&1; then
    echo "⚠️  Beaver Board is already running!"
    echo "   Opening browser..."
    open "http://localhost:5230"
    exit 0
fi

echo "🚀 Starting Beaver Board..."
open -a "$APP_PATH"

# Wait for server to start
echo "⏳ Waiting for server to start..."
sleep 3

# Open browser
open "http://localhost:5230"

echo "✅ Beaver Board is running at http://localhost:5230"
echo "   Press Ctrl+C to stop (or close the app window)"

# Keep script running
wait