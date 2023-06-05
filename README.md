# Image Upload Resize

Cloned from https://github.com/Azure-Samples/function-image-upload-resize

Azure function to resize images. Specifically:

- When an image is added to `owp-local-images` container in `owplocalimages` cloud storage account,
- ...image is resized to these 3 sizes:
  - 96px width
  - 512px width
  - 900px width
- ...and stored in `owp-local-thumbs` container

# Setup

Instructions: https://learn.microsoft.com/en-us/azure/event-grid/resize-images-on-storage-blob-upload-event?tabs=azure-cli

## For Local Dev Environment

Following steps were used to set up a Function in EXAMIND's Azure account for local dev testing. Resource names and regions will need to be modified for production.

### Create storage account for function app:

```
az storage account create --name "owplocalresize" --location "eastus" --resource-group "owp-local" --sku Standard_LRS --kind StorageV2  --allow-blob-public-access true
```

### Create function app:

```
az functionapp create --name "owp-gpt-image-resize" --storage-account "owplocalresize" --resource-group "owp-local" --consumption-plan-location "eastus" --functions-version 4
```

### Print out blob (not function) storage account connection string:

```
az storage account show-connection-string --name "owplocalimages" --resource-group "owp-local"
```

### Give function app access to storage account (use connection string from above) and set environment variables

_Note: Thumbnails must be stored in a different container than the source, otherwise, the resized images will trigger the resize event, causing an infinite loop._

Make sure to create the thumbnail container in Azure (named `owp-local-thumbs` in the example below) before executing the next step.

```
az functionapp config appsettings set --name "owp-gpt-image-resize" --resource-group "owp-local" --settings AzureWebJobsStorage=$storageConnectionString THUMBNAIL_CONTAINER_NAME=owp-local-thumbs THUMBNAIL_WIDTHS="96,512,900" FUNCTIONS_EXTENSION_VERSION=~2 FUNCTIONS_WORKER_RUNTIME=dotnet
```

### Deploy:

```
az functionapp deployment source config --name "owp-gpt-image-resize" --resource-group "owp-local" --branch main --manual-integration --repo-url https://github.com/examind-ai/owp-gpt-image-resize
```

### Trigger redeployment:

```
az functionapp deployment source sync --name "owp-gpt-image-resize" --resource-group "owp-local"
```

### Create event subscription:

Follow remainder of instructions here: https://learn.microsoft.com/en-us/azure/event-grid/resize-images-on-storage-blob-upload-event?tabs=azure-cli

Only noteworthy difference is for `Subject Begins With` filter, which should be `/blobServices/default/containers/owp-local-images/` to reflect our images container name of `owp-local-images`.

May encounter this error when trying to create subscription:

![image](https://github.com/examind-ai/owp-gpt-image-resize/assets/504505/3893ddcc-8d34-4004-a964-98399a3843f0)

If that happens, register `Microsoft.EventGrid` from Azure's Portal:
![image](https://github.com/examind-ai/owp-gpt-image-resize/assets/504505/ff644313-e930-4e42-b6a6-17f4ba3bf962)

## Local Debug

Instructions: https://learn.microsoft.com/en-us/azure/azure-functions/functions-event-grid-blob-trigger?pivots=programming-language-csharp

Copy `local.settings.exmaple.json` to `local.settings.json`. Populate `AzureWebJobsStorage` value.

Open project in Visual Studio IDE. <kbd>F5</kbd> to debug the project.

```
ngrok http 7071
```

Follow instructions under `Create the event subscription`: https://learn.microsoft.com/en-us/azure/azure-functions/functions-event-grid-blob-trigger?pivots=programming-language-csharp#create-the-event-subscription

Create the event subscription in `owp-local` resource group.

Noteworth differences from the instructions:

- `Subject Begins With` filter should be `/blobServices/default/containers/owp-local-images/` to reflect our images container name of `owp-local-images`.
- `Filter to Event Types` menu items have changed. We want `Resource Write Success`.

The webhook URL we want is: https://{alias}.ngrok.app/runtime/webhooks/EventGrid?functionName=Thumbnail

Note: I couldn't get debugging to work. It seems `Resource Write Success` is a problem when we really want `Blob Created` (which doesn't exist). Using the ngrok inspector, there's no data.url, which is required for the input stream. This was the error:

> System.Private.CoreLib: Exception while executing function: Thumbnail. Microsoft.Azure.WebJobs.Host: Exception binding parameter 'input'. Microsoft.Azure.WebJobs.Host: Error while accessing 'url': property doesn't exist.
