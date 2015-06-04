#!/bin/bash
while true; do
  until mono ./WLNetwork.exe; do
    echo "Server crashed with exit code $?.  RESPAWNING.." >&2
    sleep 1
  done
done
