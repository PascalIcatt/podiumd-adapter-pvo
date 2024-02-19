# Contactverzoeken
## Contactverzoeken bestaan niet als losse entiteit in de e-Suite
Een contactverzoek is in de e-Suite een uitgebreide versie van een contactmoment. Op basis van de aanwezig velden in de contactmoment POST wordt deze ofwel als contactmoment ofwel als contactverzoek opgeslagen. De aparte call vanuit KISS/PodiumD Contact voor het aanmaken van een contactverzoek wordt genegeerd.

# Afdelingen en Groepen
## Groepen en afdelingen hebben géén relatie in de e-Suite
KISS gaat uit van een hierarchische relatie tussen afdelingen en groepen: een groep is altijd gekoppeld aan een afdeling. Deze relatie bestaat niet in de e-Suite. 
* Het [schema van het objecttype Groep](https://github.com/open-objecten/objecttypes/blob/ce63bce09f2a9e2f2e0f42b774e17c7504327e41/community-concepts/Afdeling%20en%20Groep/groep-schema.json) is aangepast, zodat het afdelingId niet meer verplicht is. 
* De e-Suite synct afdelingen en groepen naar een Objectenregister
* PodiumD Contact vraag afdelingen op uit het Objectenregister, via de adapter
* de adapter voegt groepen en afdelingen samen in één lijst
* de adapter past de naam van de objecten aan, zodat duidelijk is of het een afdeling of een groep is (pre- of suffix bij de naam).
* Bij het wegschrijven van een contactverzoek zal de adapter o.b.v. het pre- of suffix de juiste naam in het juiste property zetten richting de e-Suite.
