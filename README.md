# ☕ CoffeeNChill
Azure Functions | Queue Triggers | Docker Compose

**CoffeeNChill – Part 2: Cloud-Based Order Processing System**

📖 Project Overview

CoffeeNChill is a cloud-based café management application developed using Microsoft Azure Functions and .NET 8. The system provides backend services for managing café menu items, staff documents and customer orders through HTTP APIs and Azure Storage services.

In Part 2, the application was extended to support **asynchronous order processing using Azure Storage Queues**. Instead of processing orders entirely within an HTTP request, the system accepts an order, places it into a queue and uses an Azure Queue Trigger to process it in the background.

The project also demonstrates containerisation using Docker, local cloud-storage emulation with Azurite, automated retry handling, exception logging and poison queue management.

The overall objective is to demonstrate a scalable, event-driven cloud application that separates order submission from background processing.

🎯 Project Objectives

- Develop RESTful HTTP endpoints using Azure Functions.
- Implement asynchronous order processing using Azure Storage Queues.
- Store and retrieve order information using Azure Table Storage.
- Track customer orders through different processing stages.
- Implement exception logging, retry handling and poison queue processing.
- Containerise the application using Docker and Docker Compose.
- Test the application using Postman and PowerShell.
- Publish the application image to Docker Hub.

🛠️ Technologies Used

| Technology | Purpose |
|---|---|
| C# / .NET 8 | Backend development |
| Azure Functions | Serverless HTTP and queue-triggered functions |
| Azure Queue Storage | Asynchronous order messaging |
| Azure Table Storage | Order and menu data persistence |
| Azure Files | Staff document storage |
| Azurite | Local Azure Storage emulation |
| Docker | Application containerisation |
| Docker Compose | Multi-container orchestration |
| Postman | API testing |
| GitHub | Source control and collaboration |
| Docker Hub | Container image publishing |

## ⚙️ System Functionality

1. Menu Management

The application provides HTTP endpoints for managing café menu items, including creating, retrieving, updating and deleting items.

Menu information is stored using Azure Table Storage.

2. Staff Document Management

The system supports uploading, listing and downloading staff documents through HTTP endpoints using Azure Files storage.

3. Asynchronous Order Processing

When a customer order is submitted, the system validates the request and places the order into an Azure Storage Queue.

The Queue Trigger then processes the message asynchronously and saves the order's status to Azure Table Storage.

**Order processing workflow:**

Customer submits order → HTTP API validates order → Azure Storage Queue → Queue Trigger → Azure Table Storage → Order status retrieved through HTTP API

The order moves through four simulated stages:

**Received → Preparing → Ready → Collected**

These stages are processed in one queue-trigger execution and do not represent real preparation times.

4. Error Handling and Queue Retries

The application uses structured exception logging to record queue-processing failures.

The Azure Functions queue configuration includes:

- Maximum dequeue count: **5**
- Visibility timeout: **10 seconds**
- Batch size: **1**

If processing repeatedly fails, Azure Functions can move the message to the `order-processing-queue-poison` queue for investigation.

🔗 API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/menu` | Retrieve menu items |
| POST | `/api/menu` | Create a menu item |
| PUT | `/api/menu/{category}/{id}` | Update a menu item |
| DELETE | `/api/menu/{category}/{id}` | Delete a menu item |
| POST | `/api/orders/queue` | Submit an order for asynchronous processing |
| GET | `/api/orders/{date}/{orderId}` | Retrieve the latest order status |

Staff document endpoints are also implemented in the application.

💻 Local Development

Prerequisites

Install the following:

- .NET 8 SDK
- Azure Functions Core Tools v4
- Docker Desktop
- Postman
- Git

### Running locally

Create `local.settings.json` from `local.settings.example.json`.

Configure `AzureWebJobsStorage` to use Azurite:

`UseDevelopmentStorage=true`

Configure the private `AzureFilesConnection` setting for staff document storage. Do not commit connection strings or credentials to GitHub.

Start Azurite, then run:

```powershell
dotnet restore
dotnet build
func start
```

The Functions API is available locally at:

`http://localhost:7071/api`

## 🐳 Docker Hub and Docker Compose

The application is containerised and published as:

**Docker image:** `isaacphiri/coffeechill-functions:v2.0`

To build and publish the image:

```powershell
docker build -t isaacphiri/coffeechill-functions:v2.0 .
docker push isaacphiri/coffeechill-functions:v2.0
```

The Docker Compose configuration runs the Azure Functions application alongside Azurite. The Functions service uses the published image.

Set the `AZURE_FILES_CONNECTION_STRING` environment variable privately before starting the containers.

```powershell
docker compose up -d
docker ps
```

To inspect queue-processing logs:

```powershell
docker compose logs -f functions
```

Persistent storage volumes allow the configured Azurite data to survive container restarts.

🧪 Testing and Verification

The application was tested using Postman, PowerShell and Docker.

| Test | Result |
| Retrieve menu items | Passed — HTTP 200 |
| Submit valid order | Passed — HTTP 202 |
| Retrieve processed order | Passed — HTTP 200 |
| Verify final order status | Passed — Collected |
| Reject invalid order | Passed — HTTP 400 |
| Queue retry and poison queue | Tested successfully |
| Exception logging | Verified in source code |
| Docker Compose startup | Passed |
| Menu data persistence after restart | Passed |
| Docker image publishing | Completed |

The successful order test confirmed that order information could be submitted, processed and retrieved from Azure Table Storage.

Invalid orders containing missing customer details, an empty SKU list and negative prices were rejected with HTTP 400 Bad Request.

📝 Part 2 Changelog

- Added the customer order model.
- Implemented the order queue producer.
- Implemented the Azure Queue Trigger consumer.
- Added Azure Table Storage order status persistence.
- Added an HTTP endpoint for retrieving order status.
- Configured automatic queue retries and poison queue handling.
- Implemented structured exception logging.
- Added Docker Compose for Azure Functions and Azurite.
- Built and published the Docker image to Docker Hub.
- Added Postman requests for order submission and status checking.
- Tested invalid-order rejection and Docker persistence.

🎥 Project Demonstration

YouTube demonstration:

The demonstration will cover Docker Compose, HTTP endpoints, queue processing, order status tracking, error handling, poison queue behaviour and the published Docker image.

📚 References

Isaac

*Azure Functions, queue processing, retries and error handling*

1. Microsoft (n.d.) *Azure Queue storage trigger for Azure Functions*. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-trigger

2. Microsoft (n.d.) *Azure Functions error handling and retries*. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-error-pages

3. Microsoft (n.d.) *Azure Functions host.json reference*. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-host-json

4. Microsoft (n.d.) *Guide for running C# Azure Functions in the isolated worker model*. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide

5. Microsoft (n.d.) *Monitoring Azure Functions*. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-monitoring

Chenge

*Docker, containerisation, networking and persistence*

1. Docker Inc. (n.d.) *Docker Compose documentation*. Available at: https://docs.docker.com/compose/

2. Docker Inc. (n.d.) *Networking in Compose*. Available at: https://docs.docker.com/compose/how-tos/networking/

3. Docker Inc. (n.d.) *Volumes*. Available at: https://docs.docker.com/engine/storage/volumes/

4. Docker Inc. (n.d.) *Docker image push*. Available at: https://docs.docker.com/reference/cli/docker/image/push/

5. Microsoft (n.d.) *Use the Azurite emulator for local Azure Storage development*. Available at: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite

Taahir

*API development, Azure Storage and Postman testing*

1. Microsoft (n.d.) *Azure Functions HTTP trigger*. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger

2. Microsoft (n.d.) *Azure Table Storage documentation*. Available at: https://learn.microsoft.com/en-us/azure/storage/tables/

3. Microsoft (n.d.) *Azure Queue storage trigger and bindings overview*. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue

4. Postman (n.d.) *Test your API using the Collection Runner*. Available at: https://learning.postman.com/docs/collections/running-collections/intro-to-collection-runs/

5. Postman (n.d.) *Write scripts to test API response data in Postman*. Available at: https://learning.postman.com/docs/tests-and-scripts/write-scripts/test-scripts/

YOUTUBE LINK - https://youtu.be/_KX9lDHVPAk
