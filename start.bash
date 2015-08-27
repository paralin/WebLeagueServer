#!/bin/bash
while true; do
  until mono ./WLNetworkRuntime.exe; do
    echo "Server crashed with exit code $?.  RESPAWNING.."
    sleep 3
  done
done
