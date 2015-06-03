#!/bin/bash
until mono ./WLNetwork.exe; do
  echo "Server crashed with exit code $?.  RESPAWNING.." >&2
  sleep 1
done
