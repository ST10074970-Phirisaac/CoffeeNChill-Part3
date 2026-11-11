# ☕ CoffeeNChill
Azure Functions | Queue Triggers | Docker Compose

**CoffeeNChill – Part 3: Cloud-Based Order Processing System**

📖 Project Overview

CoffeeNChill is a cloud-based café management application developed using Microsoft .NET 8. The system provides backend services for managing café menu items, staff documents and customer orders through HTTP APIs and Azure Storage services.

In Part 3, the application was upgraded to a secure microservices architecture and deployed to a Linux VPS.

The project demonstrates containerisation using Docker, local cloud-storage emulation with Azurite, automated retry handling, exception logging, poison queue management, API Gateway routing, JWT authentication, Role-Based Access Control (RBAC), and a fully automated CI/CD pipeline.

🎯 Project Objectives

- Develop RESTful HTTP endpoints using Azure Functions.
- Implement asynchronous order processing using Azure Storage Queues.
- Store and retrieve order information using Azure Table Storage.
- Implement an API Gateway for centralized routing and JWT authentication.
- Enforce Role-Based Access Control (Admin, Staff, Customer).
- Containerise the full application stack using Docker and Docker Compose.
- Deploy the application to a Linux VPS.
- Set up a CI/CD pipeline using GitHub Actions.
- Test the application using Swagger and Postman.

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
| API Gateway | Centralised routing and JWT authentication |
| GitHub Actions | CI/CD automation |
| Swagger | API documentation |
| Postman | API testing |
| Docker Hub | Container image publishing |

## ⚙️ System Functionality

1. Menu Management
The application provides HTTP endpoints for managing café menu items. Menu information is stored using Azure Table Storage.

2. Staff Document Management
The system supports uploading, listing, downloading and deleting staff documents through HTTP endpoints using Azure Files storage.

3. Asynchronous Order Processing
When a customer order is submitted, the system validates the request and places the order into an Azure Storage Queue. The Queue Trigger then processes the message asynchronously and saves the order's status to Azure Table Storage.

The order moves through four simulated stages:
**Received → Preparing → Ready → Collected**

4. API Gateway & Security
The Gateway sits in front of the Azure Functions and handles:
- JWT authentication using `Microsoft.AspNetCore.Authentication.JwtBearer`.
- Role-Based Access Control (RBAC) restricting endpoints to `Admin`, `Staff`, or `Customer`.
- Centralized error handling.
- Swagger API documentation.

---

## 🏗️ Part 3: System Architecture, Security & Deployment

The application is deployed to a Linux VPS using Docker Compose. External traffic is strictly routed through the API Gateway, which handles JWT authentication and Role-Based Access Control (RBAC). The Functions and Azurite containers have no public ports exposed, ensuring they can only be accessed internally by the Gateway.

**Architecture Flow:**
Client (`Port 5000`) → **API Gateway** (JWT Auth & RBAC) → **Azure Functions** (Internal `Port 80`) → **Azurite** (Internal Storage)

### 🔐 JWT & RBAC Security
- **Public Access:** Customers can view the menu, queue orders (`POST /api/orders/queue`), and check order statuses without an account.
- **Protected Access:** Updating order statuses (`PUT /api/orders/status`) and managing staff documents (`/api/documents/*`) require a valid JWT with the `Admin` or `Staff` role.
- **Live Swagger URL:** [http://172.209.208.71:5000/swagger](http://172.209.208.71:5000/swagger)

### 🐳 Containerisation & Security
- **Gateway Image:** `chengedock/coffeechill-gateway:v1.0`
- **Functions Image:** `chengedock/coffeechill-functions:v3.0`

### 🚀 CI/CD Pipeline (Bonus)
A GitHub Actions workflow (`.github/workflows/deploy.yml`) is configured. On every push to the `main` branch, it automatically builds both Docker images, pushes them to Docker Hub, and SSHes into the Linux VPS to execute `docker compose pull && docker compose up -d` to deploy the latest changes.

---

🔗 API Endpoints

| Method | Endpoint | Description | Auth Required |
|---|---|---|---|
| GET | `/api/menu` | Retrieve menu items | No |
| POST | `/api/menu` | Create a menu item | Staff/Admin |
| PUT | `/api/menu/{category}/{id}` | Update a menu item | Staff/Admin |
| DELETE | `/api/menu/{category}/{id}` | Delete a menu item | Staff/Admin |
| POST | `/api/orders/queue` | Submit an order | Customer |
| GET | `/api/orders/{date}/{orderId}` | Retrieve order status | Customer |
| PUT | `/api/orders/status` | Update order status | Staff/Admin |
| POST | `/api/documents/upload` | Upload staff document | Staff/Admin |
| GET | `/api/documents` | List staff documents | Staff/Admin |
| GET | `/api/documents/download/{fileName}`| Download document | Staff/Admin |
| DELETE | `/api/documents/{fileName}` | Delete document | Staff/Admin |
| POST | `/api/auth/register` | Register user | No |
| POST | `/api/auth/login` | Login and get JWT | No |

💻 Local Development

Prerequisites: .NET 8 SDK, Azure Functions Core Tools v4, Docker Desktop, Postman, Git.

To run locally using Docker Compose:
```powershell
docker compose up -d --build

🧪 Testing and Verification

The application was tested using Postman, Swagger and Docker.

Test	Result
Retrieve menu items via Gateway	Passed — HTTP 200
Submit valid order	Passed — HTTP 202
Retrieve processed order	Passed — HTTP 200
Reject invalid order	Passed — HTTP 400
JWT Login and RBAC	Passed
Queue retry and poison queue	Tested successfully
Docker Compose startup	Passed
CI/CD Deployment to VPS	Passed
📝 Part 3 Changelog

Added API Gateway with JWT authentication and RBAC.

Added Gateway Controllers (Auth, Menu, Orders, Documents, Health).

Added missing Functions endpoints (PUT /api/orders/status, DELETE /api/documents/{fileName}).

Created Dockerfile for Gateway and Functions.

Updated docker-compose.yml to include Gateway, Azurite, and Functions.

Exposed only port 5000 to the public.

Configured GitHub Actions CI/CD pipeline to deploy to a Linux VPS.

Updated README with architecture and security details.

🎥 Project Demonstration

YouTube demonstration: https://youtu.be/j3nFka2284I

🤖 AI Declaration

AI (specifically, an AI assistant) was used to troubleshoot and assist with the configuration of the CI/CD pipeline (GitHub Actions), Dockerfile setups, and VPS deployment troubleshooting. The core application logic, API architecture, and database operations were written by the team.

📚 References

Isaac
Azure Functions, queue processing, retries and error handling

Microsoft (n.d.) Azure Queue storage trigger for Azure Functions. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-trigger

Microsoft (n.d.) Azure Functions error handling and retries. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-error-pages

Microsoft (n.d.) Azure Functions host.json reference. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-host-json

Microsoft (n.d.) Guide for running C# Azure Functions in the isolated worker model. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide

Microsoft (n.d.) Monitoring Azure Functions. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-monitoring

Chenge
Docker, containerisation, networking and persistence

Docker Inc. (n.d.) Docker Compose documentation. Available at: https://docs.docker.com/compose/

Docker Inc. (n.d.) Networking in Compose. Available at: https://docs.docker.com/compose/how-tos/networking/

Docker Inc. (n.d.) Volumes. Available at: https://docs.docker.com/engine/storage/volumes/

Docker Inc. (n.d.) Docker image push. Available at: https://docs.docker.com/reference/cli/docker/image/push/

Microsoft (n.d.) Use the Azurite emulator for local Azure Storage development. Available at: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite

Taahir
API development, Azure Storage and Postman testing

Microsoft (n.d.) Azure Functions HTTP trigger. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger

Microsoft (n.d.) Azure Table Storage documentation. Available at: https://learn.microsoft.com/en-us/azure/storage/tables/

Microsoft (n.d.) Azure Queue storage trigger and bindings overview. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue

Postman (n.d.) Test your API using the Collection Runner. Available at: https://learning.postman.com/docs/collections/running-collections/intro-to-collection-runs/

Postman (n.d.) Write scripts to test API response data in Postman. Available at: https://learning.postman.com/docs/tests-and-scripts/write-scripts/test-scripts/
