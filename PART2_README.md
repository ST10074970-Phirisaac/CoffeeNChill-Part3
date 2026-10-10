# CoffeeNChill Part 2 – Queue Triggers and Docker Compose

## Added functionality
- `POST /api/orders/queue`: validates an order and enqueues JSON to `order-processing-queue` (HTTP 202).
- `ProcessOrderQueue`: queue-triggered Function persists status changes to the `Orders` Azure Table using PartitionKey = UTC order date (`yyyy-MM-dd`) and RowKey = OrderId.
- `GET /api/orders/{date}/{orderId}`: fetches an order's most recent status from the table (HTTP 200 or 404).
- Statuses: Received → Preparing → Ready → Collected. Transitions occur quickly in a single trigger execution; logs show every stage. This is a simulation, not a real preparation timer.

## Local development
Install .NET 8, Azure Functions Core Tools v4, Docker Desktop and Postman. Start Azurite (ports 10000-10002), create `local.settings.json` from `local.settings.example.json`, and set `AzureWebJobsStorage` to `UseDevelopmentStorage=true`. Set `AzureFilesConnection` to the **private** Azure Files storage connection string used for `staff-docs`. Run `dotnet restore`, `dotnet build`, `func start`. Import `CoffeeNChill.postman_collection.json`. POST the sample order, then GET `/api/orders/2026-10-10/ORD-2026-8801` after processing. Change the OrderId for each new submission to avoid overwriting an existing row.

## Docker Hub and Docker Compose
From the project directory, run `docker build -t isaacphiri/coffeechill-functions:v2.0 .` then `docker push isaacphiri/coffeechill-functions:v2.0` after signing into Docker Hub with permission to publish. The Compose file deliberately uses `image:` rather than `build:` for the Functions service to satisfy the rubric. Set the `AZURE_FILES_CONNECTION_STRING` environment variable **privately**, then run `docker compose up -d`. The app should be reachable at `http://localhost:7071/api`. If an older standalone Azurite container already owns ports 10000-10002, stop it before running Compose. For the demo, verify the queue trigger logs with `docker compose logs -f functions` and the Orders table in Azurite.

## Part 2 changelog
- Added order model, queue producer, queue consumer, Orders table persistence, and status lookup API.
- Added queue extension and storage queue SDK.
- Added Docker Compose for Azurite and published Docker Hub Functions image.
- Updated Postman collection with order requests.

## Testing and Verification

### Queue Processing
- Tested POST /api/orders/queue using Postman.
- Confirmed valid orders return HTTP 202 Accepted.
- Verified order statuses: Received -> Preparing -> Ready -> Collected.
- Confirmed order status is stored in Azure Table Storage.

### Error Handling and Poison Queue
- Configured maxDequeueCount to 5.
- Configured visibilityTimeout to 10 seconds.
- Tested invalid queue message INVALID_ORDER_TEST.
- Confirmed the failed message moved to order-processing-queue-poison after five attempts.
- Verified the poison queue contained the failed message.

### Docker Verification
- Rebuilt and published isaacphiri/coffeechill-functions:v2.0 to Docker Hub.
- Started Azure Functions and Azurite using Docker Compose.
- Confirmed both containers were running.
- Tested order ORD-2026-8804 through the Docker-hosted API.
- Confirmed HTTP 202 Accepted and final status Collected.

### Security Verification
- Confirmed local.settings.json and .env are not tracked by Git.
- Verified the Git working tree was clean before updating documentation.
