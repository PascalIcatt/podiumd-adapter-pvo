[![Build and Tests](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/docker-image.yaml/badge.svg)](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/docker-image.yaml)
[![Code quality checks](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/linter.yml/badge.svg)](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/linter.yml)

## Run from Visual Studio  
Be sure to set-up environment variables first
1. Make sure you've installed Docker Desktop version 4.19.0 or newer (preferably with WSL2 if using windows) and visual studio version 17.5.5 or newer
2. Set docker-compose as the single startup project

## Run in combination with PodiumD Contact (KISS) from Visual Studio
1. Run both from their own docker containers on Windows with Docker Desktop (see 'Run from Visual Studio')
2. Update the podiumD Contact env.local with the Adapter url: http://host.docker.internal:56090. use this url for the Zaken, Contactmomenten and Klanten API's (don't remove sub paths)
3. Change their API keys and id's to the id and key of an e-Suite instance. 

