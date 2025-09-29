#!/bin/bash
# Script to set up Legion Toolkit PPA repository

set -e

echo "Setting up Legion Toolkit PPA repository..."

# Add PPA repository
sudo add-apt-repository -y ppa:legion-toolkit/stable
sudo apt-get update

# Install Legion Toolkit
sudo apt-get install -y legion-toolkit

echo "Legion Toolkit installed successfully from PPA!"