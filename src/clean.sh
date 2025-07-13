#!/bin/bash

# Find and remove bin and obj directories from current directory
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} +

echo "bin and obj folders deleted."
echo "Press any key to continue..."
read -n 1 -s