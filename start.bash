#!/bin/bash
while true; do
  until mono ./WLNetwork.exe; do
    echo "Server crashed with exit code $?.  RESPAWNING.."
    sleep 3
  done
done
