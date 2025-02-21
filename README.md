[![Build and Tests](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/docker-image.yaml/badge.svg)](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/docker-image.yaml)
[![Code quality checks](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/linter.yml/badge.svg)](https://github.com/ICATT-Menselijk-Digitaal/podiumd-adapter/actions/workflows/linter.yml)


Adapter for any application using Common Ground standard API's that needs to communicate with the e-Suite API's. Offers translations for Zaken, Zaaktypen, Klanten, Contactmomenten. Developed by [ICATT. Menselijk digitaal](https://www.icatt.nl) for Dimpact.


## Run from Visual Studio  
Be sure to set-up environment variables first
1. Make sure you've installed Docker Desktop version 4.19.0 or newer (preferably with WSL2 if using windows) and visual studio version 17.5.5 or newer
2. Set docker-compose as the single startup project

## Run in combination with PodiumD Contact (KISS) from Visual Studio
1. Run both from their own docker containers on Windows with Docker Desktop (see 'Run from Visual Studio')
2. Update the podiumD Contact env.local with the Adapter url: http://host.docker.internal:56090. use this url for the Zaken, Contactmomenten and Klanten API's (don't remove sub paths)
3. In the development environment there is no authentication on calls to the Adapter. Any technically valid value for the API keys and id's in PodiumD Contact will do. 

# Installation in Kubernetes
Ideally, you would set up the following in a CI/CD pipeline.
If you were to install the adapter by hand, you can follow these steps:

## 1. Come up with values for the environment variables
### All values are mandatory, for array values you need to specify at least one.
```powershell
# Specify the image tag to use. we will recommend one when we reach a stable version
$IMAGE_TAG='main-latest'

# OPTIONAL: If you specify a host and a secret name, we will set up an Ingress
$INGRESS_HOST='www.my-website.nl'
$INGRESS_SECRET_NAME='the-name-of-the-secret-in-kubernetes-to-use-for-tls'

# The base url of the API's that the E-Suite offers. 
# For example, the Klanten API is presumed to exist at /klanten-api-provider/api/v1
$ESUITE_BASE_URL='https://www.my-e-suite.nl'

# You need to create an application in the E-Suite at /beheer/zgwapisproviderbeheer/applicaties
$ESUITE_CLIENT_ID='my-client-id'
# IMPORTANT The secret needs to be at least 32 characters
$ESUITE_CLIENT_SECRET='abc$%^&*defg1234567abc$%^&*defg1'

# Each client what will connect to the e-Suite (klanten, contactmomenten, zaken and catalogi) parts of the podiumd-adapter must do so using a Bearer token,
# following the convention used in Open Zaak:
# https://open-zaak.readthedocs.io/en/stable/client-development/authentication.html
# For the OverigeObjecten (Interne taak) part of the podiumd-adapter you only need te specify a client secret.
# Client IDs are set up using the CLIENTS__INDEX__ID naming convention
# You will need to set up at least 1 Client
$CLIENTS__0__ID='MY-CLIENT-ID-FOR-EXAMPLE-KISS'
$CLIENTS__1__ID='ANOTHER-CLIENT-ID-IF-APPLICABLE'

# Just like with the client ids, you can set up multiple using the CLIENTS__INDEX__SECRET naming convention
# Secrets have a minimum length of 16 characters
# For the Interne taak part of the podiumd-adapter the client secrets are specified in the same way.
$CLIENTS__0__SECRET='zyxu908237^%&*zyxu908237^%&*'
$CLIENTS__1__SECRET='erfg94367!@$erfg94367!@$'

# Configure the Contact Types in the E-Suite that correspond with a contactverzoek in KISS
# Use the same naming convention for arrays as above
$CONTACTVERZOEK_TYPES__0='Terugbelverzoek'

# Configure the object types from the object types registration. 
# These need to match the values you've configured for KISS. 
# The adapter uses the same endpoint for interne taken, afdelingen and groepen and medewerkers,
# and uses the object types to determine how to handle each request correctly
$INTERNE_TAAK_OBJECT_TYPE_URL='https://www.my-obect-types-registration.nl/api/v2/objecttypes/1df73259-1a58-4180-bf98-598eefc184d4'
$SMOELENBOEK_OBJECT_TYPE_URL='https://www.my-obect-types-registration.nl/api/v2/objecttypes/748dd17b-2eb6-4084-95f0-a9040e27dced'

# Configure the base url for your objects registration and the token you've configured in the objects registration 
$SMOELENBOEK_BASE_URL='https://www.my-objects-registration.nl'
$SMOELENBOEK_TOKEN='0982309823498@#$@#$'

```
## 2. Create a `namespace`
```powershell
kubectl create namespace podiumd-adapter-namespace
```
## 3. Create a `config map`
```powershell
kubectl -n podiumd-adapter-namespace create configmap podiumd-adapter-config `
--from-literal=ESUITE_BASE_URL=$ESUITE_BASE_URL `
--from-literal=INTERNE_TAAK_OBJECT_TYPE_URL=$INTERNE_TAAK_OBJECT_TYPE_URL `
--from-literal=SMOELENBOEK_BASE_URL=$SMOELENBOEK_BASE_URL `
--from-literal=SMOELENBOEK_OBJECT_TYPE_URL=$SMOELENBOEK_OBJECT_TYPE_URL `
--from-literal=CONTACTVERZOEK_TYPES__0=$CONTACTVERZOEK_TYPES__0
```
## 4. Create a `secret`
```powershell
kubectl -n podiumd-adapter-namespace create secret generic podiumd-adapter-secrets `
--from-literal=ESUITE_CLIENT_ID=$ESUITE_CLIENT_ID `
--from-literal=ESUITE_CLIENT_SECRET=$ESUITE_CLIENT_SECRET `
--from-literal=CLIENTS__0__ID=$CLIENTS__0__ID `
--from-literal=CLIENTS__0__SECRET=$CLIENTS__0__SECRET `
--from-literal=CLIENTS__1__ID=$CLIENTS__1__ID `
--from-literal=CLIENTS__1__SECRET=$CLIENTS__1__SECRET `
--from-literal=SMOELENBOEK_TOKEN=$SMOELENBOEK_TOKEN
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

# You need an objects registration. For an example on getting that to work, see `/helm/objecten.md`
