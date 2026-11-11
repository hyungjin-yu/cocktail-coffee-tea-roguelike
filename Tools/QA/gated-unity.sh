#!/usr/bin/env bash
# Usage: gated_unity.sh <unity args...>  — refuses to start Unity unless free RAM >= 4096MB and no Unity Editor is running.
RAM=$(powershell -NoProfile -c "[int]((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1024)" | tr -d '\r')
EDITORS=$(powershell -NoProfile -c "(Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { \$_.Path -like '*Hub\\Editor*' }).Count" | tr -d '\r')
echo "GATE free_ram_mb=$RAM editors_running=${EDITORS:-0} at $(date '+%H:%M:%S')"
if [ -z "$RAM" ] || [ "$RAM" -lt 4096 ]; then echo "GATE REFUSED: RAM < 4096"; exit 99; fi
if [ -n "$EDITORS" ] && [ "$EDITORS" -gt 0 ]; then echo "GATE REFUSED: editor already running"; exit 98; fi
"/c/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" "$@"
echo "EXIT=$?"
