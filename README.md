# Aspose.PDF Exporter for GitHub

Aspose.PDF Exporter for Github is a free  application for [Github Marketplace](https://github.com/marketplace) that allow users to export their issues to pdf. Powered by [Aspose.PDF Cloud](https://products.aspose.cloud/pdf/family) and [Aspose.BarCode Cloud](https://products.aspose.cloud/barcode/family).
**NOTE:**
This project contains only backend functionality. Frontend is located in [another](https://github.com/aspose-pdf-cloud/aspose-pdf-exporter-for-github-frontend) project. To build bundle read [Bundle](#bundle) section

## Project structure
* *src/AsposePdfExporterGitHub/AsposePdfExporterGitHub* - Aspose.PDF Exporter app's source code
* *src/AsposePdfExporterGitHub/AsposePdfExporterGitHub.Tests* - Aspose.PDF Exporter tests
* *src/AsposePdfExporterGitHub/AsposePdfExporterGitHub.IntegrationTests* - Aspose.PDF Exporter integration tests

## AsposePdfExporterGitHub structure
* *Controllers* contains backend controllers classes (Setup, Repository, Export)
* *Model* - helper classes used internally to represent entities
* *Services* - services used in the app
* *template* contains Yaml templates for report generator
* wwwroot contains simple webpages used mostly for testing/experimenting. Usually wwwroot's content is replaced with Frontend's assets  

AsposePdfExporterGitHub heavily use [Application Services](https://github.com/aspose-pdf-cloud/application-services) libraries set. 

## Project description

Please read how to [Build Apps for Hithub](https://developer.github.com/apps/) first in order to get overview how to integrate web applications to Github.

Frontend uses [Github OAuth flow](https://developer.github.com/apps/building-oauth-apps/authorizing-oauth-apps/) and exchanges `code` for an `access_token` using `/token` backend call (defined in *Setup* controller). As result it receives Github's `access_token` as well as additional user info (login, name, avatar url, etc).

All other backend methods are protected by authorization, i.e. frontend has to pass `Authorization: Token <access_token>` header.


## Configuration
You must prepare `appsettings.Development.json` file with required options to run
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information"
    }
  },
  "Settings": {
    "AppName": "aspose-pdf-exporter-app",
    "BaseAppUrl": "<BASE_APP_URL>"
  },
  "AsposeCloud": {
    "ApiKey": "<ASPOSE_CLOUD_APIKEY>",
    "AppSid": "<ASPOSE_CLOUD_APPSID>"
  },
  "GithubApp": {
    "ClientId": "<GITHUB_CLIENT_ID>",
    "ClientSecret": "<GITHUB_CLIENT_SECRET>"
  },
  "Elasticsearch": {
    "Uris": [ "<ELASTICSEARCH_URI>" ],
    "apiId": "<ELASTICSEARCH_APIID>",
    "apiKey": "<ELASTICSEARCH_APPKEY>"
  }
}

```

Where

* *<GITHUB_CLIENT_ID>* and *<GITHUB_CLIENT_SECRET>* Your app's Client Id and secret obtained in [Github](https://github.com/settings/developers) OAUTH pane
* *<ASPOSECLOUD_API_KEY>* and *<ASPOSECLOUD_APP_SID>*  are used to access [Aspose Cloud](https://www.aspose.cloud/). Should be obtained through [Dashboard](https://dashboard.aspose.cloud)
* *<ELASTICSEARCH_URL>*, *<ELASTICSEARCH_APIID>*, *<ELASTICSEARCH_APPKEY>* Elasticsearch URL to post logging and error documents. **"Elasticsearch"** section is optional

## Bundle

* Checkout [Frontend](https://github.com/aspose-pdf-cloud/aspose-pdf-exporter-for-github-frontend)
* Build frontend
* Copy files in Frontend's *dist/pdf-exporter-github* to wwwroot

## Docker

You can build image using `docker build  -t githubexporter`
