#!/bin/bash
set -e

echo "Downloading simulator ..."
dotnet tool restore
dotnet pxl
