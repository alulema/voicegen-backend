## `local.settings.json` format

`{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "CosmosDBEndpoint": "https://voice-generator-db.documents.azure.com:443/",
        "CosmosDBPrimaryKey": "secret_key",
        "DatabaseId": "VoiceGeneratorDb",
        "ContainerId": "Users",
        "GOOGLE_CLIENT_ID": "secret_key",
        "OpenAIApiKey": "secret_key",
        "TrialMaximum": 3
    },
    "Host": {
        "CORS": "*"
    }
}`