[![Build and Tests](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/docker-image.yaml/badge.svg)](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/docker-image.yaml)
[![Code quality checks](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/linter.yml/badge.svg)](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/linter.yml)

## Run from Visual Studio  
Be sure to set-up environment variables first
1. Make sure you've installed Docker Desktop version 4.19.0 or newer (preferably with WSL2 if using windows) and visual studio version 17.5.5 or newer
2. Set docker-compose as the single startup project

## Run in combination with PodiumD Contact (KISS) from Visual Studio
1. Run both from their own docker containers on Windows with Docker Desktop (see 'Run from Visual Studio')
2. Update the podiumD Contact env.local with the Adapter url: http://host.docker.internal:56090. use this url for the Zaken, Contactmomenten and Klanten API's (don't remove sub paths)
3. In the development environment there is no authentication on calls to the Adapter. Any technically valid value for the API keys and id's in PodiumD Contact will do. 

# Installation in Kubernetes
## 1. Come up with values for the environment variables
```powershell
# Specify the image tag to use. we will recommend one when we reach a stable version
$IMAGE_TAG='main-latest'

# OPTIONAL: If you specify a host and a secret name, we will set up an Ingress
$INGRESS_HOST='www.my-website.nl'
$INGRESS_SECRET_NAME='the-name-of-the-secret-in-kubernetes-to-use-for-tls'

# The base url of the API's that the E-Suite offers. 
# For example, the Klanten API is presumed to exist at /klanten-api-provider/api/v1
$ESUITE_BASE_URL='https://www.my-e-suite.nl'

# You need to generate this in the E-Suite
$ESUITE_TOKEN='abc$%^&*defg1234567'

# Each client what will connect to the podiumd-adapter must do so using a Bearer token,
# following the convention used in Open Zaak:
# https://open-zaak.readthedocs.io/en/stable/client-development/authentication.html
# Client IDs are set up using the CLIENTS__INDEX__ID naming convention
# You will need to set up at least 1 Client
$CLIENTS__0__ID='MY-CLIENT-ID-FOR-EXAMPLE-KISS'
$CLIENTS__1__ID='ANOTHER-CLIENT-ID-IF-APPLICABLE'

# Just like with the client ids, you can set up multiple using the CLIENTS__INDEX__SECRET naming convention
# Secrets have a minimum length of 16 characters
$CLIENTS__0__SECRET='zyxu908237^%&*zyxu908237^%&*'
$CLIENTS__1__SECRET='erfg94367!@$erfg94367!@$'
```
## 2. Create a `namespace`
```powershell
kubectl create namespace podiumd-adapter-namespace
```
## 3. Create a `config map`
```powershell
kubectl -n podiumd-adapter-namespace create configmap podiumd-adapter-config `
--from-literal=ESUITE_BASE_URL=$ESUITE_BASE_URL
```
## 4. Create a `secret`
```powershell
kubectl -n podiumd-adapter-namespace create secret generic podiumd-adapter-secrets `
--from-literal=ESUITE_TOKEN=$ESUITE_TOKEN `
--from-literal=CLIENTS__0__ID=$CLIENTS__0__ID `
--from-literal=CLIENTS__0__SECRET=$CLIENTS__0__SECRET `
--from-literal=CLIENTS__1__ID=$CLIENTS__1__ID `
--from-literal=CLIENTS__1__SECRET=$CLIENTS__1__SECRET
```
## 5. Use the `helm chart` to deploy the application
```powershell
helm repo add podiumd-adapter https://raw.githubusercontent.com/ICATT-Menselijk-Digitaal/podiumd-adapter/main/helm
helm repo update
helm upgrade --install podiumd-adapter-ci-release podiumd-adapter `
--namespace podiumd-adapter-namespace `
--version 0.1.0 `
--set image.tag=$IMAGE_TAG `
--set ingress.host=$INGRESS_HOST `
--set ingress.secretName=$INGRESS_SECRET_NAME
```
