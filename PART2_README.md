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

## Host Configuration (`host.json`)
The Azure Functions host configuration is configured with queue retry and visibility parameters in `host.json`:

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      },
      "enableLiveMetricsFilters": true
    }
  },
  "extensions": {
    "queues": {
      "maxDequeueCount": 5,
      "visibilityTimeout": "00:00:10",
      "batchSize": 1
    }
  }
}
```

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


---

## Docker Compose — Persistence & Networking Verification

All commands below were executed on the working branch after building the image locally from source. The committed `docker-compose.yml` continues to use `image: isaacphiri/coffeechill-functions:v2.0` as required by the rubric; local verification was performed using a git-ignored `docker-compose.override.yml` that swaps the image for a local build. The `docker-compose.yml` improvements committed alongside this document are the Azurite `healthcheck`, the `depends_on: condition: service_healthy` gating for the Functions container, and the explicit `--location /data` argument on Azurite.

### Networking Verification

**3.1 — Docker DNS resolves the `azurite` service name from the Functions container**

docker compose exec functions getent hosts azurite
172.19.0.2 azurite


The Functions container resolves the service name `azurite` to the container's IP on the Compose-managed bridge network. This is the same hostname referenced in the connection string used by the Functions app (`http://azurite:10000`, `http://azurite:10001`, `http://azurite:10002`), so all queue, blob, and table traffic from the Functions container reaches Azurite without relying on host networking.

**3.2 — Both containers are on the same Compose network**

docker network inspect coffeenchill---part2_default --format "{{range .Containers}}{{.Name}} {{.IPv4Address}}{{println}}{{end}}"
coffeenchill---part2-azurite-1 172.19.0.2/16
coffeenchill---part2-functions-1 172.19.0.3/16


Both containers are attached to the default network created by Compose for this project (`coffeenchill---part2_default`), on adjacent addresses in the `172.19.0.0/16` subnet.

**3.3 — Host-to-Functions port mapping (`7071:80`)**

curl.exe -v http://localhost:7071/api/orders/2026-10-12/ORD-PERSIST-0001
< HTTP/1.1 200 OK
< Content-Type: application/json; charset=utf-8
< Server: Kestrel
{"OrderId":"ORD-PERSIST-0001","OrderDate":"2026-10-12","Status":"Collected","CustomerName":"Test User","SelectedItemSKUs":["SKU-123"],"TotalPrice":10,"UpdatedAt":"2026-10-12T11:59:47.7394644+00:00"}


The Functions host is reachable from the host machine at `http://localhost:7071` via the Compose port mapping, and the HTTP trigger responds with the expected payload.

**3.4 — Host-to-Azurite port mapping (`10000:10000`)**

curl.exe -v "http://localhost:10000/devstoreaccount1?comp=list"
< HTTP/1.1 403 Server failed to authenticate the request.
< Server: Azurite-Blob/3.37.0


The blob service of Azurite is reachable from the host at `localhost:10000`. The 403 is expected — no auth header was supplied — and the `Server: Azurite-Blob/3.37.0` response header confirms the port mapping is live and Azurite is answering.

### Persistence Verification

**Step 1 — Store an order, then tear down containers (keeping the volume)**

curl.exe -X POST http://localhost:7071/api/orders/queue -H "Content-Type: application/json" -d $body
{"OrderId":"ORD-PERSIST-0001","status":"Queued","message":"Order accepted for asynchronous processing"}

curl.exe http://localhost:7071/api/orders/2026-10-12/ORD-PERSIST-0001
{"OrderId":"ORD-PERSIST-0001","OrderDate":"2026-10-12","Status":"Collected","CustomerName":"Test User","SelectedItemSKUs":["SKU-123"],"TotalPrice":10,"UpdatedAt":"2026-10-12T11:59:47.7394644+00:00"}

docker compose down
[+] down 3/3
✔ Container coffeenchill---part2-functions-1 Removed
✔ Container coffeenchill---part2-azurite-1 Removed
✔ Network coffeenchill---part2_default Removed


Note the absence of a "Removing volume" line: the named volume `azurite_data` is intentionally preserved across `down`.

**Step 2 — Confirm the named volume still exists**

docker volume ls | findstr azurite
local coffeenchill---part2_azurite_data


**Step 3 — Recreate the stack**

docker compose up -d
[+] up 3/3
✔ Network coffeenchill---part2_default Created
✔ Container coffeenchill---part2-azurite-1 Healthy
✔ Container coffeenchill---part2-functions-1 Started


**Step 4 — Fetch the order that was written before the teardown**

curl.exe http://localhost:7071/api/orders/2026-10-12/ORD-PERSIST-0001
{"OrderId":"ORD-PERSIST-0001","OrderDate":"2026-10-12","Status":"Collected","CustomerName":"Test User","SelectedItemSKUs":["SKU-123"],"TotalPrice":10,"UpdatedAt":"2026-10-12T11:59:47.7394644+00:00"}


The order that was written before `docker compose down` is still returned after `docker compose up -d`, including the identical `UpdatedAt` timestamp. This confirms that Azurite's data is stored on the named volume `coffeenchill---part2_azurite_data` and is preserved across container lifecycles.

### Conclusion

- **Networking:** the Functions container resolves the `azurite` service by name via Docker's embedded DNS; both containers share the same Compose network; and the host machine can reach both services through the published ports (`7071` and `10000-10002`).
- **Persistence:** the named volume `coffeenchill---part2_azurite_data` survives a full `docker compose down` and `docker compose up -d` cycle. Data written to Table Storage before the teardown is intact and retrievable afterwards.