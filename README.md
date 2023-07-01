# Image Upload Resize

Azure Function to automatically resize images when they're uploaded to blob storage.

Cloned from https://github.com/Azure-Samples/function-image-upload-resize with the following customizations:

- Resize image to multiple widths (not just one as in the original)
- Fix bug that skewed image ratio if resize scale was not a whole number - PR for fix submitted [here](https://github.com/Azure-Samples/function-image-upload-resize/pull/24), but not merged upstream as of this writing
- Set content-type of resized images to enable browsers to provide functionality based on file type
- Set cache-control header of resized images to save bandwidth and improve performance when clients load images

## Deployment

### First time

```
az functionapp deployment source config `
  --name $funcAppName `
  --resource-group $resourceGroup `
  --branch main `
  --manual-integration `
  --repo-url https://github.com/examind-ai/owp-gpt-image-resize
```

### Subsequent

```
az functionapp deployment source sync `
  --name $funcAppName `
  --resource-group $resourceGroup
```

See `owp-gpt-devops/deploy_image_resize_function.ps1` for scripts.

### Configure EventGrid

https://learn.microsoft.com/en-us/azure/event-grid/resize-images-on-storage-blob-upload-event?tabs=azure-cli

### Environment Variables

_Note: Resized images must be stored in a different container than the source, otherwise the resized images will trigger the resize event, causing an infinite loop._

```
az functionapp config appsettings set `
  --name --name $funcAppName `
  --resource-group "$resourceGroup
  --settings AzureWebJobsStorage=$storageConnectionString ` THUMBNAIL_CONTAINER_NAME=$thumbNameContainerName `
  THUMBNAIL_WIDTHS="96,512,900" `
  FUNCTIONS_EXTENSION_VERSION=~2 `
  FUNCTIONS_WORKER_RUNTIME=dotnet
```
